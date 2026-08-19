using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace pk3DS.Core.CTR
{
    /// <summary>
    /// Pure C# ARM (ARMv5/ARMv6) instruction encoder and decoder.
    /// Covers the ~25 instruction forms used in USUM CRO modding.
    /// Eliminates the Keystone/Capstone native DLL dependency.
    /// </summary>
    public static class ARMCodec
    {
        #region Condition Codes
        private static readonly Dictionary<string, uint> CondCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["EQ"] = 0x0, ["NE"] = 0x1, ["CS"] = 0x2, ["HS"] = 0x2,
            ["CC"] = 0x3, ["LO"] = 0x3, ["MI"] = 0x4, ["PL"] = 0x5,
            ["VS"] = 0x6, ["VC"] = 0x7, ["HI"] = 0x8, ["LS"] = 0x9,
            ["GE"] = 0xA, ["LT"] = 0xB, ["GT"] = 0xC, ["LE"] = 0xD,
            ["AL"] = 0xE, [""]   = 0xE
        };

        private static readonly string[] CondNames =
            { "EQ","NE","CS","CC","MI","PL","VS","VC","HI","LS","GE","LT","GT","LE","","NV" };
        #endregion

        #region Register Parsing
        private static readonly Dictionary<string, int> RegMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["R0"]=0,["R1"]=1,["R2"]=2,["R3"]=3,["R4"]=4,["R5"]=5,["R6"]=6,["R7"]=7,
            ["R8"]=8,["R9"]=9,["R10"]=10,["R11"]=11,["R12"]=12,["R13"]=13,["R14"]=14,["R15"]=15,
            ["SP"]=13,["LR"]=14,["PC"]=15,
            ["SL"]=10,["FP"]=11,["IP"]=12
        };

        private static int ParseReg(string s)
        {
            s = s.Trim().TrimEnd(',').Trim();
            if (RegMap.TryGetValue(s, out int r)) return r;
            throw new ArgumentException($"Unknown register: '{s}'");
        }

        private static ushort ParseRegList(string s)
        {
            // Format: {r0, r1, r4-r7, lr}
            s = s.Trim().TrimStart('{').TrimEnd('}').Trim();
            ushort mask = 0;
            foreach (string part in s.Split(','))
            {
                string p = part.Trim();
                if (p.Contains('-'))
                {
                    var range = p.Split('-');
                    int lo = ParseReg(range[0]);
                    int hi = ParseReg(range[1]);
                    for (int i = lo; i <= hi; i++) mask |= (ushort)(1 << i);
                }
                else if (!string.IsNullOrWhiteSpace(p))
                {
                    mask |= (ushort)(1 << ParseReg(p));
                }
            }
            return mask;
        }
        #endregion

        #region Immediate Encoding
        /// <summary>
        /// Encode a 32-bit immediate value into the ARM Imm8r4 format (8-bit value + 4-bit rotation).
        /// Returns (rotation, imm8) or null if the value cannot be encoded.
        /// </summary>
        /// <summary>
        /// Encode a 32-bit value as the ARM "8-bit immediate rotated right by 2*rot" form, or null
        /// when the value has no such representation.
        /// <para>
        /// The rotate field returned is the one <see cref="DecodeImm8r4"/> consumes, i.e. the value
        /// is reconstructed as <c>imm8 ROR (2*rot)</c>. Finding the byte requires rotating the
        /// value in the opposite direction, so the search index has to be negated before it is
        /// returned; previously it was returned as-is, which made encode/decode disagree — e.g.
        /// 0x3C0 encoded to (rot 3, 0x0F) but decoded back as 0x3C000000. Values whose lowest set
        /// bit sits at an odd position (0x420, say) genuinely cannot be encoded, and now correctly
        /// return null instead of a wrong answer.
        /// </para>
        /// </summary>
        public static (uint rot, uint imm8)? EncodeImm8r4(uint value)
        {
            (uint rot, uint imm8)? best = null;
            for (uint r = 0; r < 16; r++)
            {
                uint shifted = RotateRight(value, (int)(r * 2));
                if (shifted > 0xFF) continue;

                uint rot = (16 - r) & 15;
                if (best == null || rot < best.Value.rot)
                    best = (rot, shifted);
            }
            return best;
        }

        private static uint RotateRight(uint val, int bits)
        {
            bits &= 31;
            return (val >> bits) | (val << (32 - bits));
        }

        private static uint RotateLeft(uint val, int bits)
        {
            bits &= 31;
            return (val << bits) | (val >> (32 - bits));
        }

        /// <summary>Decode Imm8r4 → 32-bit value.</summary>
        public static uint DecodeImm8r4(uint rot, uint imm8)
        {
            return RotateRight(imm8, (int)(rot * 2));
        }

        public static uint ParseImmediate(string s)
        {
            s = s.Trim().TrimStart('#').Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || s.StartsWith("0X"))
                return uint.Parse(s[2..], NumberStyles.HexNumber);
            if (s.StartsWith("-"))
                return (uint)(-(int.Parse(s)));
            return uint.Parse(s);
        }
        #endregion

        #region Assembler (string → bytes)
        /// <summary>
        /// Assemble one or more ARM instructions. Lines separated by \n or ;.
        /// Labels (ending with :) are supported for branch targets.
        /// Returns machine code bytes or null on failure.
        /// </summary>
        public static byte[] Assemble(string asmText, uint baseAddress = 0)
        {
            if (string.IsNullOrWhiteSpace(asmText)) return null;

            // Pre-process: strip comments, split lines
            var rawLines = asmText.Replace(";", "\n").Split('\n')
                .Select(l => l.Trim())
                .Select(l => {
                    int commentIdx = l.IndexOf('@');
                    return commentIdx >= 0 ? l[..commentIdx].Trim() : l;
                })
                .Where(l => !string.IsNullOrEmpty(l))
                .ToList();

            // First pass: collect labels and instruction indices
            var labels = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
            var instructions = new List<string>();

            foreach (string line in rawLines)
            {
                if (line.EndsWith(':'))
                {
                    string label = line[..^1].Trim();
                    labels[label] = baseAddress + (uint)(instructions.Count * 4);
                }
                else
                {
                    instructions.Add(line);
                }
            }

            // Second pass: assemble each instruction
            var result = new List<byte>();
            for (int i = 0; i < instructions.Count; i++)
            {
                uint pc = baseAddress + (uint)(i * 4);
                uint word = AssembleInstruction(instructions[i], pc, labels);
                result.AddRange(BitConverter.GetBytes(word));
            }
            return result.ToArray();
        }

        private static uint AssembleInstruction(string line, uint pc, Dictionary<string, uint> labels)
        {
            // Parse condition code suffix
            string mnemonic = ExtractMnemonic(line, out string cond, out string operands);
            uint condBits = CondCodes.TryGetValue(cond, out uint cc) ? cc : 0xE;

            string mn = mnemonic.ToUpperInvariant();

            return mn switch
            {
                "NOP" => 0xE1A00000, // MOV R0, R0
                "B" => EncodeBranch(condBits, operands, pc, labels, false),
                "BL" => EncodeBranch(condBits, operands, pc, labels, true),
                "BX" => EncodeBX(condBits, operands),
                "MOV" => EncodeDataProc(condBits, 0xD, operands, false),
                "MVN" => EncodeDataProc(condBits, 0xF, operands, false),
                "MOVS" => EncodeDataProc(condBits, 0xD, operands, true),
                "CMP" => EncodeCompare(condBits, 0xA, operands),
                "CMN" => EncodeCompare(condBits, 0xB, operands),
                "TST" => EncodeCompare(condBits, 0x8, operands),
                "TEQ" => EncodeCompare(condBits, 0x9, operands),
                "ADD" => EncodeDataProc(condBits, 0x4, operands, false),
                "ADDS" => EncodeDataProc(condBits, 0x4, operands, true),
                "SUB" => EncodeDataProc(condBits, 0x2, operands, false),
                "SUBS" => EncodeDataProc(condBits, 0x2, operands, true),
                "RSB" => EncodeDataProc(condBits, 0x3, operands, false),
                "AND" => EncodeDataProc(condBits, 0x0, operands, false),
                "ANDS" => EncodeDataProc(condBits, 0x0, operands, true),
                "ORR" => EncodeDataProc(condBits, 0xC, operands, false),
                "EOR" => EncodeDataProc(condBits, 0x1, operands, false),
                "BIC" => EncodeDataProc(condBits, 0xE, operands, false),
                "MUL" => EncodeMul(condBits, operands),
                "UXTH" => EncodeExtend(condBits, operands, 0x06FF0070),
                "UXTB" => EncodeExtend(condBits, operands, 0x06EF0070),
                "SXTH" => EncodeExtend(condBits, operands, 0x06BF0070),
                "SXTB" => EncodeExtend(condBits, operands, 0x06AF0070),
                "LDR" => EncodeLoadStore(condBits, operands, true, 4),
                "STR" => EncodeLoadStore(condBits, operands, false, 4),
                "LDRB" => EncodeLoadStore(condBits, operands, true, 1),
                "STRB" => EncodeLoadStore(condBits, operands, false, 1),
                "LDRH" => EncodeLoadStoreHalf(condBits, operands, true),
                "STRH" => EncodeLoadStoreHalf(condBits, operands, false),
                "PUSH" => EncodePushPop(condBits, operands, true),
                "POP" => EncodePushPop(condBits, operands, false),
                "LSL" => EncodeShift(condBits, operands, 0),
                "LSR" => EncodeShift(condBits, operands, 1),
                "ASR" => EncodeShift(condBits, operands, 2),
                "ROR" => EncodeShift(condBits, operands, 3),
                _ => throw new ArgumentException($"Unknown mnemonic: {mnemonic}")
            };
        }

        private static string ExtractMnemonic(string line, out string cond, out string operands)
        {
            int spaceIdx = line.IndexOf(' ');
            string full;
            if (spaceIdx < 0)
            {
                full = line;
                operands = "";
            }
            else
            {
                full = line[..spaceIdx];
                operands = line[(spaceIdx + 1)..].Trim();
            }

            // Check for condition code suffix (2 chars at end of mnemonic)
            cond = "";
            string upper = full.ToUpperInvariant();

            // Special: PUSH, POP, UXTH, UXTB, SXTH, SXTB, LDRH, STRH, LDRB, STRB — don't strip
            string[] noStrip = { "PUSH","POP","UXTH","UXTB","SXTH","SXTB","LDRH","STRH","LDRB","STRB","ADDS","SUBS","MOVS","ANDS","NOP","MUL" };
            if (noStrip.Contains(upper)) return upper;

            // B and BL need special handling (BEQ, BLEQ, etc.)
            if (upper.Length >= 3 && upper.StartsWith("BL") && upper != "BL" && upper != "BLX" && upper != "BIC")
            {
                string suffix = upper[2..];
                if (CondCodes.ContainsKey(suffix)) { cond = suffix; return "BL"; }
            }
            if (upper.Length >= 2 && upper.StartsWith("B") && upper != "BL" && upper != "BX" && upper != "BIC")
            {
                string suffix = upper[1..];
                if (CondCodes.ContainsKey(suffix)) { cond = suffix; return "B"; }
            }

            // General: 3+ char mnemonic, last 2 might be condition
            if (upper.Length >= 4)
            {
                string suffix = upper[^2..];
                if (CondCodes.ContainsKey(suffix))
                {
                    string base_mn = upper[..^2];
                    // Only strip if base is a known mnemonic
                    string[] known = { "MOV","MVN","CMP","CMN","TST","TEQ","ADD","SUB","RSB","AND","ORR","EOR","BIC","LDR","STR","MUL","LSL","LSR","ASR","ROR" };
                    if (known.Contains(base_mn)) { cond = suffix; return base_mn; }
                }
            }

            return upper;
        }

        private static uint EncodeBranch(uint cond, string operands, uint pc, Dictionary<string, uint> labels, bool link)
        {
            uint target;
            string op = operands.Trim().Replace("0x","").Replace("0X","");

            if (labels != null && labels.TryGetValue(operands.Trim(), out uint labelAddr))
            {
                target = labelAddr;
            }
            else if (operands.Trim().StartsWith("#"))
            {
                target = ParseImmediate(operands);
            }
            else
            {
                // Try parse as hex address
                if (!uint.TryParse(op, NumberStyles.HexNumber, null, out target))
                    target = uint.Parse(op);
            }

            int offset = (int)target - (int)(pc + 8);
            uint offset24 = (uint)(offset >> 2) & 0x00FFFFFF;
            uint opcode = link ? 0x0B000000u : 0x0A000000u;
            return (cond << 28) | opcode | offset24;
        }

        private static uint EncodeBX(uint cond, string operands)
        {
            int rm = ParseReg(operands.Trim());
            return (cond << 28) | 0x012FFF10 | (uint)rm;
        }

        private static uint EncodeDataProc(uint cond, uint opcode, string operands, bool setFlags)
        {
            var parts = operands.Split(',').Select(s => s.Trim()).ToArray();

            int rd = ParseReg(parts[0]);
            uint s = setFlags ? (1u << 20) : 0;

            // MOV/MVN: Rd, Op2 (no Rn)
            if (opcode == 0xD || opcode == 0xF)
            {
                uint op2 = EncodeOp2(parts.Length > 1 ? parts[1] : "0", parts.Length > 2 ? parts[2] : null);
                bool isImm = parts.Length > 1 && parts[1].Trim().StartsWith('#');
                uint iBit = isImm ? (1u << 25) : 0;
                return (cond << 28) | iBit | (opcode << 21) | s | ((uint)rd << 12) | op2;
            }

            // ADD, SUB, etc.: Rd, Rn, Op2
            int rn = ParseReg(parts[1]);
            string op2Str = parts.Length > 2 ? parts[2] : "0";
            string shiftStr = parts.Length > 3 ? parts[3] : null;
            uint op2val = EncodeOp2(op2Str, shiftStr);
            bool isImm2 = op2Str.Trim().StartsWith('#');
            uint iBit2 = isImm2 ? (1u << 25) : 0;
            return (cond << 28) | iBit2 | (opcode << 21) | s | ((uint)rn << 16) | ((uint)rd << 12) | op2val;
        }

        private static uint EncodeCompare(uint cond, uint opcode, string operands)
        {
            var parts = operands.Split(',').Select(s => s.Trim()).ToArray();
            int rn = ParseReg(parts[0]);
            string op2Str = parts.Length > 1 ? parts[1] : "0";
            string shiftStr = parts.Length > 2 ? parts[2] : null;
            uint op2val = EncodeOp2(op2Str, shiftStr);
            bool isImm = op2Str.Trim().StartsWith('#');
            uint iBit = isImm ? (1u << 25) : 0;
            // S bit always set for CMP/CMN/TST/TEQ
            return (cond << 28) | iBit | (opcode << 21) | (1u << 20) | ((uint)rn << 16) | op2val;
        }

        private static uint EncodeOp2(string operand, string shift)
        {
            operand = operand.Trim();
            if (operand.StartsWith('#'))
            {
                uint val = ParseImmediate(operand);
                var enc = EncodeImm8r4(val);
                if (enc == null)
                    throw new ArgumentException($"Cannot encode immediate 0x{val:X} in Imm8r4 format");
                return (enc.Value.rot << 8) | enc.Value.imm8;
            }
            else
            {
                int rm = ParseReg(operand);
                uint shiftField = 0;
                if (!string.IsNullOrWhiteSpace(shift))
                {
                    shiftField = EncodeShiftField(shift.Trim());
                }
                return shiftField | (uint)rm;
            }
        }

        private static uint EncodeShiftField(string shift)
        {
            // "LSL #5", "ASR #5", "LSL r2", etc.
            var parts = shift.Split(new[] { ' ', '#' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return 0;

            uint shiftType = parts[0].ToUpperInvariant() switch
            {
                "LSL" => 0, "LSR" => 1, "ASR" => 2, "ROR" => 3,
                _ => throw new ArgumentException($"Unknown shift: {parts[0]}")
            };

            if (RegMap.ContainsKey(parts[1].ToUpperInvariant()))
            {
                int rs = ParseReg(parts[1]);
                return ((uint)rs << 8) | (shiftType << 5) | (1 << 4);
            }
            else
            {
                uint amt = uint.Parse(parts[1].TrimStart('#'));
                return (amt << 7) | (shiftType << 5);
            }
        }

        private static uint EncodeMul(uint cond, string operands)
        {
            var parts = operands.Split(',').Select(s => s.Trim()).ToArray();
            int rd = ParseReg(parts[0]);
            int rm = ParseReg(parts[1]);
            int rs = parts.Length > 2 ? ParseReg(parts[2]) : 0;
            // MUL Rd, Rm, Rs
            return (cond << 28) | ((uint)rd << 16) | ((uint)rs << 8) | 0x90u | (uint)rm;
        }

        private static uint EncodeExtend(uint cond, string operands, uint baseOpcode)
        {
            var parts = operands.Split(',').Select(s => s.Trim()).ToArray();
            int rd = ParseReg(parts[0]);
            int rm = ParseReg(parts[1]);
            return (cond << 28) | baseOpcode | ((uint)rd << 12) | (uint)rm;
        }

        private static uint EncodeLoadStore(uint cond, string operands, bool load, int size)
        {
            // LDR Rd, [Rn, #imm]  or  LDR Rd, [Rn, Rm]  or  LDR Rd, [Rn]  or  LDR Rd, [PC, #imm]
            var match = Regex.Match(operands, @"(\w+)\s*,\s*\[(\w+)(?:\s*,\s*(.+?))?\](!)?");
            if (!match.Success) throw new ArgumentException($"Bad LDR/STR operands: {operands}");

            int rd = ParseReg(match.Groups[1].Value);
            int rn = ParseReg(match.Groups[2].Value);
            string offStr = match.Groups[3].Success ? match.Groups[3].Value.Trim() : null;
            bool writeback = match.Groups[4].Success;

            bool up = true;
            uint offset = 0;
            bool isReg = false;

            if (offStr != null)
            {
                if (offStr.StartsWith('#') || offStr.StartsWith("-#") || offStr.StartsWith("-"))
                {
                    string clean = offStr.TrimStart('#');
                    if (clean.StartsWith("-"))
                    {
                        up = false;
                        clean = clean.TrimStart('-').TrimStart('#');
                    }
                    if (clean.StartsWith("0x") || clean.StartsWith("0X"))
                        offset = uint.Parse(clean[2..], NumberStyles.HexNumber);
                    else
                        offset = uint.Parse(clean);
                }
                else
                {
                    isReg = true;
                    if (offStr.StartsWith("-"))
                    {
                        up = false;
                        offStr = offStr.TrimStart('-');
                    }
                    offset = (uint)ParseReg(offStr);
                }
            }

            if (size == 1) // LDRB/STRB
            {
                uint word = (cond << 28) | (1u << 26);
                if (!isReg) word |= (1u << 25); // P bit (pre-indexed)
                // Wait, for immediate offset: I=0, P=1, U=up, B=1, W=writeback, L=load
                word = (cond << 28) | (0x01u << 26);
                word |= (1u << 24); // P (pre-indexed)
                if (up) word |= (1u << 23);
                word |= (1u << 22); // B (byte)
                if (writeback) word |= (1u << 21);
                if (load) word |= (1u << 20);
                word |= ((uint)rn << 16) | ((uint)rd << 12);
                if (!isReg)
                    word |= offset & 0xFFF;
                else
                    word |= (1u << 25) | offset;
                return word;
            }
            else // LDR/STR (word)
            {
                uint word = (cond << 28) | (0x01u << 26);
                word |= (1u << 24); // P
                if (up) word |= (1u << 23);
                if (writeback) word |= (1u << 21);
                if (load) word |= (1u << 20);
                word |= ((uint)rn << 16) | ((uint)rd << 12);
                if (!isReg)
                    word |= offset & 0xFFF;
                else
                    word |= (1u << 25) | offset;
                return word;
            }
        }

        private static uint EncodeLoadStoreHalf(uint cond, string operands, bool load)
        {
            var match = Regex.Match(operands, @"(\w+)\s*,\s*\[(\w+)(?:\s*,\s*(.+?))?\](!)?");
            if (!match.Success) throw new ArgumentException($"Bad LDRH/STRH operands: {operands}");

            int rd = ParseReg(match.Groups[1].Value);
            int rn = ParseReg(match.Groups[2].Value);
            string offStr = match.Groups[3].Success ? match.Groups[3].Value.Trim() : null;

            bool up = true;
            uint offset = 0;

            if (offStr != null)
            {
                string clean = offStr.TrimStart('#');
                if (clean.StartsWith("-")) { up = false; clean = clean.TrimStart('-').TrimStart('#'); }
                if (clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    offset = uint.Parse(clean[2..], NumberStyles.HexNumber);
                else
                    offset = uint.Parse(clean);
            }

            // Halfword: P=1, U, I=1(imm), W=0, L, Rn, Rd, ImmHi(4), 1011, ImmLo(4)
            uint word = (cond << 28);
            word |= (1u << 24); // P
            if (up) word |= (1u << 23);
            word |= (1u << 22); // I (immediate offset)
            if (load) word |= (1u << 20);
            word |= ((uint)rn << 16) | ((uint)rd << 12);
            word |= ((offset >> 4) & 0xF) << 8;
            word |= 0xB0; // 1011 pattern
            word |= offset & 0xF;
            return word;
        }

        private static uint EncodePushPop(uint cond, string operands, bool push)
        {
            ushort regList = ParseRegList(operands);
            if (push) // STMDB SP!, {reglist}
                return (cond << 28) | 0x092D0000 | regList;
            else      // LDMIA SP!, {reglist}
                return (cond << 28) | 0x08BD0000 | regList;
        }

        private static uint EncodeShift(uint cond, string operands, uint shiftType)
        {
            // LSL Rd, Rm, #imm  or  LSL Rd, Rm, Rs
            var parts = operands.Split(',').Select(s => s.Trim()).ToArray();
            int rd = ParseReg(parts[0]);
            int rm = parts.Length > 1 ? ParseReg(parts[1]) : rd;
            if (parts.Length > 2)
            {
                string amt = parts[2].Trim();
                if (amt.StartsWith('#'))
                {
                    uint immAmt = ParseImmediate(amt);
                    return (cond << 28) | 0x01A00000 | ((uint)rd << 12) | (immAmt << 7) | (shiftType << 5) | (uint)rm;
                }
                else
                {
                    int rs = ParseReg(amt);
                    return (cond << 28) | 0x01A00000 | ((uint)rd << 12) | ((uint)rs << 8) | (shiftType << 5) | (1u << 4) | (uint)rm;
                }
            }
            return (cond << 28) | 0x01A00000 | ((uint)rd << 12) | (shiftType << 5) | (uint)rm;
        }
        #endregion

        #region Disassembler (bytes → string)
        /// <summary>
        /// Disassemble a block of ARM machine code into human-readable assembly.
        /// </summary>
        public static string Disassemble(byte[] code, uint baseAddress = 0)
        {
            var lines = new List<string>();
            for (int i = 0; i + 3 < code.Length; i += 4)
            {
                uint word = BitConverter.ToUInt32(code, i);
                uint pc = baseAddress + (uint)i;
                lines.Add($"0x{pc:X8}:  {DisassembleWord(word, pc)}");
            }
            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>Disassemble a single 32-bit ARM instruction word.</summary>
        public static string DisassembleWord(uint word, uint pc = 0)
        {
            uint cond = word >> 28;
            string cs = cond < 14 ? CondNames[cond] : "";

            // NOP
            if (word == 0xE1A00000) return "NOP";
            if ((word & 0x0FFFFFFF) == 0x01A00000 && cond == 0xE) return "NOP";

            // Branch (B/BL)
            if ((word & 0x0E000000) == 0x0A000000)
            {
                bool link = (word & 0x01000000) != 0;
                int offset = (int)(word & 0x00FFFFFF);
                if ((offset & 0x800000) != 0) offset |= unchecked((int)0xFF000000); // Sign extend
                uint target = (uint)(pc + 8 + (offset << 2));
                return $"{(link ? "BL" : "B")}{cs} 0x{target:X8}";
            }

            // BX
            if ((word & 0x0FFFFFF0) == 0x012FFF10)
            {
                int rm = (int)(word & 0xF);
                return $"BX{cs} R{rm}";
            }

            // Data Processing
            if ((word & 0x0C000000) == 0x00000000)
            {
                uint opcode = (word >> 21) & 0xF;
                bool s = (word & (1 << 20)) != 0;
                bool i = (word & (1 << 25)) != 0;
                int rn = (int)((word >> 16) & 0xF);
                int rd = (int)((word >> 12) & 0xF);

                // MUL
                if (!i && (word & 0x0FC000F0) == 0x00000090)
                {
                    int rm2 = (int)(word & 0xF);
                    int rs2 = (int)((word >> 8) & 0xF);
                    int rd2 = (int)((word >> 16) & 0xF);
                    return $"MUL{cs} R{rd2}, R{rm2}, R{rs2}";
                }

                // Halfword load/store check
                if (!i && (word & 0x0E000090) == 0x00000090 && (word & 0x00000060) != 0)
                {
                    return DisassembleHalfword(word, cs);
                }

                // UXTH/UXTB/SXTH/SXTB
                if ((word & 0x0FF00FF0) == 0x06FF0070) { int rdx = (int)((word >> 12) & 0xF); int rmx = (int)(word & 0xF); return $"UXTH{cs} R{rdx}, R{rmx}"; }
                if ((word & 0x0FF00FF0) == 0x06EF0070) { int rdx = (int)((word >> 12) & 0xF); int rmx = (int)(word & 0xF); return $"UXTB{cs} R{rdx}, R{rmx}"; }
                if ((word & 0x0FF00FF0) == 0x06BF0070) { int rdx = (int)((word >> 12) & 0xF); int rmx = (int)(word & 0xF); return $"SXTH{cs} R{rdx}, R{rmx}"; }
                if ((word & 0x0FF00FF0) == 0x06AF0070) { int rdx = (int)((word >> 12) & 0xF); int rmx = (int)(word & 0xF); return $"SXTB{cs} R{rdx}, R{rmx}"; }

                string op2 = i ? DecodeImmOp2(word) : DecodeRegOp2(word);

                string[] dpNames = { "AND","EOR","SUB","RSB","ADD","ADC","SBC","RSC","TST","TEQ","CMP","CMN","ORR","MOV","BIC","MVN" };
                string name = dpNames[opcode];
                string sf = s && opcode < 8 ? "S" : "";

                // TST/TEQ/CMP/CMN: no Rd
                if (opcode >= 8 && opcode <= 11)
                    return $"{name}{cs} R{rn}, {op2}";

                // MOV/MVN: no Rn
                if (opcode == 0xD || opcode == 0xF)
                    return $"{name}{sf}{cs} R{rd}, {op2}";

                return $"{name}{sf}{cs} R{rd}, R{rn}, {op2}";
            }

            // LDR/STR/LDRB/STRB
            if ((word & 0x0C000000) == 0x04000000)
            {
                bool load = (word & (1 << 20)) != 0;
                bool isByte = (word & (1 << 22)) != 0;
                bool up = (word & (1 << 23)) != 0;
                bool pre = (word & (1 << 24)) != 0;
                int rd3 = (int)((word >> 12) & 0xF);
                int rn3 = (int)((word >> 16) & 0xF);
                uint offset3 = word & 0xFFF;
                bool isReg = (word & (1 << 25)) != 0;

                string mn = (load ? "LDR" : "STR") + (isByte ? "B" : "") + cs;
                string sign = up ? "" : "-";

                if (!isReg)
                {
                    if (offset3 == 0)
                        return $"{mn} R{rd3}, [R{rn3}]";
                    return $"{mn} R{rd3}, [R{rn3}, #{sign}0x{offset3:X}]";
                }
                else
                {
                    int rm3 = (int)(word & 0xF);
                    return $"{mn} R{rd3}, [R{rn3}, {sign}R{rm3}]";
                }
            }

            // Block Transfer (PUSH/POP/LDM/STM)
            if ((word & 0x0E000000) == 0x08000000)
            {
                bool load = (word & (1 << 20)) != 0;
                bool up = (word & (1 << 23)) != 0;
                bool pre = (word & (1 << 24)) != 0;
                bool wb = (word & (1 << 21)) != 0;
                int rn4 = (int)((word >> 16) & 0xF);
                ushort regList = (ushort)(word & 0xFFFF);

                // PUSH = STMDB SP!, {...}  POP = LDMIA SP!, {...}
                if (rn4 == 13 && wb)
                {
                    if (!load && pre && !up) return $"PUSH{cs} {{{RegListStr(regList)}}}";
                    if (load && !pre && up) return $"POP{cs} {{{RegListStr(regList)}}}";
                }

                string mode = (pre ? (up ? "IB" : "DB") : (up ? "IA" : "DA"));
                string wbStr = wb ? "!" : "";
                return $"{(load ? "LDM" : "STM")}{mode}{cs} R{rn4}{wbStr}, {{{RegListStr(regList)}}}";
            }

            return $".word 0x{word:X8}";
        }

        private static string DisassembleHalfword(uint word, string cs)
        {
            bool load = (word & (1 << 20)) != 0;
            bool up = (word & (1 << 23)) != 0;
            bool isImm = (word & (1 << 22)) != 0;
            int rd = (int)((word >> 12) & 0xF);
            int rn = (int)((word >> 16) & 0xF);
            uint sh = (word >> 5) & 3;
            string[] types = { "???", "H", "SB", "SH" };
            string mn = (load ? "LDR" : "STR") + types[sh] + cs;

            if (isImm)
            {
                uint immHi = (word >> 8) & 0xF;
                uint immLo = word & 0xF;
                uint offset = (immHi << 4) | immLo;
                string sign = up ? "" : "-";
                if (offset == 0)
                    return $"{mn} R{rd}, [R{rn}]";
                return $"{mn} R{rd}, [R{rn}, #{sign}0x{offset:X}]";
            }
            else
            {
                int rm = (int)(word & 0xF);
                string sign = up ? "" : "-";
                return $"{mn} R{rd}, [R{rn}, {sign}R{rm}]";
            }
        }

        private static string DecodeImmOp2(uint word)
        {
            uint rot = (word >> 8) & 0xF;
            uint imm8 = word & 0xFF;
            uint val = DecodeImm8r4(rot, imm8);
            return $"#0x{val:X}";
        }

        private static string DecodeRegOp2(uint word)
        {
            int rm = (int)(word & 0xF);
            uint shiftType = (word >> 5) & 3;
            bool regShift = (word & (1 << 4)) != 0;
            string[] shiftNames = { "LSL", "LSR", "ASR", "ROR" };

            if (regShift)
            {
                int rs = (int)((word >> 8) & 0xF);
                return $"R{rm}, {shiftNames[shiftType]} R{rs}";
            }
            else
            {
                uint amt = (word >> 7) & 0x1F;
                if (amt == 0 && shiftType == 0) return $"R{rm}";
                return $"R{rm}, {shiftNames[shiftType]} #{amt}";
            }
        }

        private static string RegListStr(ushort mask)
        {
            var regs = new List<string>();
            for (int i = 0; i < 16; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    string name = i switch { 13 => "SP", 14 => "LR", 15 => "PC", _ => $"R{i}" };
                    regs.Add(name);
                }
            }
            return string.Join(", ", regs);
        }
        #endregion

        #region Utility
        /// <summary>
        /// Generate a B (branch) instruction from one absolute address to another.
        /// </summary>
        public static byte[] EncodeBranch(uint from, uint to) =>
            BitConverter.GetBytes(0xEA000000 | ((uint)((int)(to - from - 8) >> 2) & 0x00FFFFFF));

        /// <summary>
        /// Generate a BL (branch-with-link) instruction from one absolute address to another.
        /// </summary>
        public static byte[] EncodeBranchLink(uint from, uint to) =>
            BitConverter.GetBytes(0xEB000000 | ((uint)((int)(to - from - 8) >> 2) & 0x00FFFFFF));

        /// <summary>
        /// Generate a conditional branch instruction.
        /// </summary>
        public static byte[] EncodeConditionalBranch(uint from, uint to, string condition, bool link = false)
        {
            uint cond = CondCodes.TryGetValue(condition, out uint cc) ? cc : 0xE;
            uint opcode = link ? 0x0B000000u : 0x0A000000u;
            uint offset24 = (uint)((int)(to - from - 8) >> 2) & 0x00FFFFFF;
            return BitConverter.GetBytes((cond << 28) | opcode | offset24);
        }

        /// <summary>Decode a B/BL target address from an instruction word.</summary>
        public static uint DecodeBranchTarget(uint word, uint pc)
        {
            int offset = (int)(word & 0x00FFFFFF);
            if ((offset & 0x800000) != 0) offset |= unchecked((int)0xFF000000);
            return (uint)(pc + 8 + (offset << 2));
        }

        /// <summary>Check if an instruction word is a B or BL.</summary>
        public static bool IsBranch(uint word) => (word & 0x0E000000) == 0x0A000000;

        /// <summary>Check if an instruction word is a BL.</summary>
        public static bool IsBranchLink(uint word) => (word & 0x0F000000) == 0x0B000000;

        /// <summary>Encode a NOP instruction.</summary>
        public static byte[] EncodeNOP() => BitConverter.GetBytes(0xE1A00000u);
        #endregion
    }
}
