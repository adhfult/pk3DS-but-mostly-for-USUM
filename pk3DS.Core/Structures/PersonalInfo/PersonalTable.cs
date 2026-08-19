using System;

namespace pk3DS.Core.Structures.PersonalInfo;

public class PersonalTable
{
    public static PersonalInfo GetInfo(byte[] data, GameVersion format)
    {
        return format switch
        {
            GameVersion.XY => new PersonalInfoXY(data),
            GameVersion.ORASDEMO or GameVersion.ORAS => new PersonalInfoORAS(data),
            GameVersion.SMDEMO or GameVersion.SM or GameVersion.USUM => new PersonalInfoSM(data),
            _ => null,
        };
    }
    private static byte[][] SplitBytes(byte[] data, int size)
    {
        byte[][] r = new byte[data.Length / size][];
        for (int i = 0; i < data.Length; i += size)
        {
            r[i / size] = new byte[size];
            Array.Copy(data, i, r[i / size], 0, size);
        }
        return r;
    }

    public PersonalTable(byte[] data, GameVersion format)
    {
        int size = format switch
        {
            GameVersion.XY => PersonalInfoXY.SIZE,
            GameVersion.ORASDEMO => PersonalInfoORAS.SIZE,
            GameVersion.ORAS => PersonalInfoORAS.SIZE,
            GameVersion.SMDEMO => PersonalInfoSM.SIZE,
            GameVersion.SM => PersonalInfoSM.SIZE,
            GameVersion.USUM => PersonalInfoSM.SIZE,
            _ => 0,
        };

        if (size == 0)
        { Table = null; return; }

        byte[][] entries = SplitBytes(data, size);
        PersonalInfo[] d = new PersonalInfo[data.Length / size];

        switch (format)
        {
            case GameVersion.XY:
                for (int i = 0; i < d.Length; i++)
                    d[i] = new PersonalInfoXY(entries[i]);
                break;
            case GameVersion.ORASDEMO:
            case GameVersion.ORAS:
                for (int i = 0; i < d.Length; i++)
                    d[i] = new PersonalInfoORAS(entries[i]);
                break;
            case GameVersion.SMDEMO:
            case GameVersion.SM:
            case GameVersion.USUM:
                for (int i = 0; i < d.Length; i++)
                    d[i] = new PersonalInfoSM(entries[i]);
                break;
        }
        Table = d;
    }

    public PersonalInfo[] Table;

    public PersonalInfo this[int index]
    {
        get
        {
            if (index < Table.Length)
                return Table[index];
            return Table[0];
        }
        set
        {
            if (index >= Table.Length)
                return;
            Table[index] = value;
        }
    }

    public int[] GetAbilities(int species, int forme)
    {
        if (species >= Table.Length)
        { species = 0; Console.WriteLine("Requested out of bounds SpeciesID"); }
        return this[GetFormIndex(species, forme)].Abilities;
    }

    public int GetFormIndex(int species, int forme)
    {
        if (species >= Table.Length)
        { species = 0; Console.WriteLine("Requested out of bounds SpeciesID"); }
        int maxSp = Table != null && Table.Length >= 1026 ? 1025 : 807;
        return this[species].FormeIndex(species, forme, maxSp);
    }

    public PersonalInfo GetFormEntry(int species, int forme)
    {
        return this[GetFormIndex(species, forme)];
    }

    public string[][] GetFormList(string[] species, int MaxSpecies)
    {
        int count = Math.Min(MaxSpecies + 1, Table != null ? Table.Length : MaxSpecies + 1);
        string[][] FormList = new string[count][];
        for (int i = 0; i < count; i++)
        {
            int FormCount = this[i].FormeCount;
            FormList[i] = new string[FormCount];
            if (FormCount <= 0) continue;
            string baseName = (i < species.Length && !string.IsNullOrEmpty(species[i])) ? species[i] : $"Species {i}";
            FormList[i][0] = baseName;
            for (int j = 1; j < FormCount; j++)
            {
                FormList[i][j] = $"{baseName} {j}";
            }
        }

        return FormList;
    }

    private static readonly System.Collections.Generic.Dictionary<int, string> CustomFormNames1280_1329 = new()
    {
        [1280] = "Cramorant 1",
        [1281] = "Cramorant 2",
        [1282] = "Toxtricity 1",
        [1283] = "Sinistea 1",
        [1284] = "Polteageist 1",
        [1285] = "Alcremie 1",
        [1286] = "Alcremie 2",
        [1287] = "Alcremie 3",
        [1288] = "Alcremie 4",
        [1289] = "Alcremie 5",
        [1290] = "Alcremie 6",
        [1291] = "Alcremie 7",
        [1292] = "Alcremie 8",
        [1293] = "Falinks 1",
        [1294] = "Eiscue 1",
        [1295] = "Indeedee 1",
        [1296] = "Morpeko 1",
        [1297] = "Zacian 1",
        [1298] = "Zamazenta 1",
        [1299] = "Eternatus 1",
        [1300] = "Urshifu 1",
        [1301] = "Zarude 1",
        [1302] = "Calyrex 1",
        [1303] = "Calyrex 2",
        [1304] = "Ursaluna 1",
        [1305] = "Basculegion 1",
        [1306] = "Enamorus 1",
        [1307] = "Oinkologne 1",
        [1308] = "Maushold 1",
        [1309] = "Squawkabilly 1",
        [1310] = "Squawkabilly 2",
        [1311] = "Squawkabilly 3",
        [1312] = "Scovillain 1",
        [1313] = "Palafin 1",
        [1314] = "Glimmora 1",
        [1315] = "Tatsugiri 1",
        [1316] = "Tatsugiri 2",
        [1317] = "Tatsugiri 3",
        [1318] = "Tatsugiri 4",
        [1319] = "Tatsugiri 5",
        [1320] = "Dudunsparce 1",
        [1321] = "Baxcalibur 1",
        [1322] = "Gimmighoul 1",
        [1323] = "Poltchageist 1",
        [1324] = "Sinistcha 1",
        [1325] = "Ogerpon 1",
        [1326] = "Ogerpon 2",
        [1327] = "Ogerpon 3",
        [1328] = "Terapagos 1",
        [1329] = "Terapagos 2"
    };

    public string[] GetPersonalEntryList(string[][] AltForms, string[] species, int MaxSpecies, out int[] baseForm, out int[] formVal)
    {
        string[] result = new string[Table.Length];
        baseForm = new int[result.Length];
        formVal = new int[result.Length];

        int shift = MaxSpecies > 807 ? (MaxSpecies - 807) : 0;

        // 1. Map Base Species entries (0 .. MaxSpecies)
        for (int i = 0; i <= MaxSpecies && i < species.Length && i < Table.Length; i++)
        {
            result[i] = !string.IsNullOrEmpty(species[i]) ? species[i] : $"Species {i}";
            baseForm[i] = i;
            formVal[i] = 0;

            if (i >= AltForms.Length || AltForms[i] == null || AltForms[i].Length <= 1) continue;
            int altformpointer = this[i].FormStatsIndex;
            if (altformpointer <= 0) continue;

            if (shift > 0 && i <= 807 && altformpointer >= 808 && altformpointer < 1026)
            {
                altformpointer += shift;
            }

            for (int j = 1; j < AltForms[i].Length; j++)
            {
                int ptr = altformpointer + j - 1;
                if (ptr >= result.Length) break;
                baseForm[ptr] = i;
                formVal[ptr] = j;
                result[ptr] = AltForms[i][j];
            }
        }

        // 2. Fallback & Custom Form Names pass for any form entries in Personal Table
        for (int k = 0; k < result.Length; k++)
        {
            if (CustomFormNames1280_1329.TryGetValue(k, out string customName))
            {
                result[k] = customName;
                string baseName = customName.Split(' ')[0];
                int sIdx = System.Array.IndexOf(species, baseName);
                if (sIdx > 0)
                {
                    baseForm[k] = sIdx;
                    int.TryParse(customName.Split(' ')[^1], out formVal[k]);
                }
            }
            else if (string.IsNullOrEmpty(result[k]))
            {
                // Reverse lookup base species and form index
                int foundSpecies = -1;
                int foundForm = 0;
                for (int s = 1; s <= MaxSpecies && s < Table.Length; s++)
                {
                    int formCount = this[s].FormeCount;
                    int altPointer = this[s].FormStatsIndex;
                    if (shift > 0 && s <= 807 && altPointer >= 808 && altPointer < 1026)
                        altPointer += shift;

                    if (formCount > 1 && altPointer > 0 && k >= altPointer && k < altPointer + formCount - 1)
                    {
                        foundSpecies = s;
                        foundForm = k - altPointer + 1;
                        break;
                    }
                }

                if (foundSpecies > 0)
                {
                    baseForm[k] = foundSpecies;
                    formVal[k] = foundForm;
                    string bName = (foundSpecies < species.Length && !string.IsNullOrEmpty(species[foundSpecies])) ? species[foundSpecies] : $"Species {foundSpecies}";
                    result[k] = $"{bName} {foundForm}";
                }
                else
                {
                    baseForm[k] = k <= MaxSpecies ? k : 0;
                    formVal[k] = 0;
                    result[k] = k <= MaxSpecies ? (k < species.Length && !string.IsNullOrEmpty(species[k]) ? species[k] : $"Species {k}") : $"Species {k}";
                }
            }
        }

        return result;
    }

    public int[] GetSpeciesForm(int PersonalEntry, GameConfig config)
    {
        if (PersonalEntry < config.MaxSpeciesID) return [PersonalEntry, 0];

        for (int i = 0; i < config.MaxSpeciesID; i++)
        {
            int FormCount = this[i].FormeCount - 1; // Mons with no alt forms have a FormCount of 1.
            var altformpointer = this[i].FormStatsIndex;
            if (altformpointer <= 0) continue;
            for (int j = 0; j < FormCount; j++)
            {
                if (altformpointer + j == PersonalEntry)
                    return [i, j];
            }
        }

        return [-1, -1];
    }
}