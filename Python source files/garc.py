

valid_game_names = ("XY", "ORAS", "SM", "USUM")
game_version = {"XY": 2, "ORAS": 2, "SM": 3, "USUM": 3}

class GARC:

    def __init__(self, file_list: list, game: str,
                 unknown_header_num: int):
    
        self.file_list = file_list
        self.game = game
        self.unknown_header_num = unknown_header_num

        if game not in valid_game_names:
            raise ValueError(f"Invalid game name \"{game}\"!")

    @classmethod
    def from_garc_file(cls, file: bytes, game: str,
                       check_if_sesd = True):

        if game not in valid_game_names:
            raise ValueError(f"Invalid game name \"{game}\"!")
        
        version = game_version[game]

        # This will consist of a LOT of validation.
        # Just really wanna make sure this is a GARC!
  

        if file[0:4] != b"CRAG":
            error_msg = ("This file does not begin with the bytes" +
                         "43524147 (CRAG).")
            raise ValueError(error_msg)

        garc_length = int.from_bytes(file[4:8],"little")

        if version == 2 and garc_length != 28:
            error_msg = (f"The GARC block's size should be 28"+
                         "for this game,\nbut is apparently {garc_length}.")
            raise ValueError(error_msg)

        elif version == 3 and garc_length != 36:
            error_msg = (f"The GARC block's size should be 36"+
                         "for this game,\nbut is apparently {garc_length}.")
            raise ValueError(error_msg)
        

        if file[8:10] != b"\xFF\xFE":
            error_msg = "Expected the value 0xFFFE at offset 0x08."
            raise ValueError(error_msg)

        if version == 2 and file[10:12] != b"\x00\x04":
            error_msg = "Expected the value 0x0004 at offset 0x0A."
            raise ValueError(error_msg)
        if version == 3 and file[10:12] != b"\x00\x06":
            error_msg = "Expected the value 0x0006 at offset 0x0A."
            raise ValueError(error_msg)
                
        if file[12:16] != b"\x04\x00\x00\x00":
            error_msg = "Expected the value 0x04000000 at offset 0x0C."
            raise ValueError(error_msg)

        first_file_offset = int.from_bytes(file[16:20],"little")

        full_garc_length = int.from_bytes(file[20:24],"little")

        unknown_header_num = int.from_bytes(file[24:28],"little")

        # That's all for the GARC block.
        # Next is the FATO block.

        if version == 2:
            crsr = 28
        elif version == 3:
            crsr = 36
        else:
            raise ValueError("Somehow the version isn't 2 or 3? How!?")
        
        if file[crsr:crsr+4] != b"OTAF":
            error_msg = ("This file does not have the bytes" +
                         "4F544146 (OTAF) where expected.")
            raise ValueError(error_msg)        
        
        
        fato_length = int.from_bytes(file[crsr+4:crsr+8],"little")

        num_elements = int.from_bytes(file[crsr+8:crsr+10],"little")

        # The elements are all just 0x10 multiplied by their index.
        # So we don't need to store them.
        expected_length = (num_elements*4) + 12
        
        if fato_length != expected_length:
            error_msg = (f"The FATO block was supposedly {fato_length},\nbut "+
                         "based on the # of elements, it's {expected_length}.")
            raise ValueError(error_msg)             
            

        
        crsr += fato_length

        # Next is the FATB block, which actually matters to us!

        if file[crsr:crsr+4] != b"BTAF":
            error_msg = ("This file does not have the bytes" +
                         "42544146 (BTAF) where expected.")
            raise ValueError(error_msg)        
        
        
        fatb_length = int.from_bytes(file[crsr+4:crsr+8],"little")

        num_elements_fatb = int.from_bytes(file[crsr+8:crsr+10],"little")
        if num_elements_fatb != num_elements:
            error_msg = ("FATB block has a different number of elements\n" +
                         f"({num_elements_fatb}) than the FATO block "+
                         f"({num_elements}).")
            raise ValueError(error_msg)

        crsr += 12

        start_offsets = []
        end_offsets = []
        file_lengths = []
        for i in range(num_elements):
            start = int.from_bytes(file[crsr+4:crsr+8],"little")
            end = int.from_bytes(file[crsr+8:crsr+12],"little")
            length = int.from_bytes(file[crsr+12:crsr+16],"little")
            start_offsets.append(start)
            end_offsets.append(end)
            file_lengths.append(length)
            crsr += 16

        # Finally we have the FIMB block: the actual file contents!

        if file[crsr:crsr+4] != b"BMIF":
            error_msg = ("This file does not have the bytes" +
                         "424D4946 (BMIF) where expected.")
            raise ValueError(error_msg)

        fimb_length = int.from_bytes(file[crsr+8:crsr+12],"little")        
        
        crsr += 12
        
        file_list = []
        for i in range(num_elements):
            start = start_offsets[i]
            end = end_offsets[i]
            length = file_lengths[i]

            curr_file = file[crsr+start:crsr+end]
            file_list.append(curr_file)

            if check_if_sesd:
                if curr_file[0:4] != b"SESD":
                    error_msg = ("File #{i} in the GARC doesn't start" +
                                 "with 53455344 (SESD) as expected.")                    
                if curr_file[-4:] != b"\xFF\xFF\xFF\xFF":
                    error_msg = ("File #{i} in the GARC doesn't end" +
                                 "with FFFFFFFF as expected.")

        return cls(file_list, game, unknown_header_num)
            
            
    def replace_file(self, index: int, file: bytes):
        self.file_list[index] = file

        
    def get_garc(self):
        version = game_version[self.game]
        num_elements = len(self.file_list)

        output = bytearray()


        # Before we actually start writing,
        # let's calculate the start & end offsets of each file.

        start_offsets = []
        end_offsets = []
        file_lengths = []
        crsr = 0
        for i in range(num_elements):
            curr_file = self.file_list[i]
            start_offsets.append(crsr)
            file_lengths.append(len(curr_file))

            # The end offset will be a bit different.
            # If the file's length isn't a multiple of 4,
            # we add padding bytes to make it so,
            # and those padding bytes ARE counted in the end offset.

            if len(curr_file)%4 != 0:
                self.file_list[i] += b"\xFF" * (4 - len(curr_file)%4)
                curr_file = self.file_list[i]

            crsr += len(curr_file)
            end_offsets.append(crsr)
            
            

        # First, we write the GARC block.

        output += b"CRAG"

        if version == 2:
            output += b"\x1C\x00\x00\x00"
            output += b"\xFF\xFE\x00\x04"
            output += b"\x04\x00\x00\x00"

            # Offset where the actual files start
            files_start = 64 + (20 * num_elements)
            output += files_start.to_bytes(4,'little')

            # Offset where the actual files end
            files_end = files_start + end_offsets[-1]
            output += files_end.to_bytes(4,'little')

            # Aaaand... whatever this is.
            output += self.unknown_header_num.to_bytes(4,'little')
            
            
        elif version == 3:
            output += b"\x24\x00\x00\x00"
            output += b"\xFF\xFE\x00\x06"
            output += b"\x04\x00\x00\x00"

            # Offset where the actual files start
            files_start = 72 + (20 * num_elements)
            output += files_start.to_bytes(4,'little')

            # Offset where the actual files end
            files_end = files_start + end_offsets[-1]
            output += files_end.to_bytes(4,'little')

            # Aaaand... whatever this is. Twice.
            output += 2*(self.unknown_header_num.to_bytes(4,'little'))
            
            output += b"\x04\x00\x00\x00"


        # GARC block done!
        # Next is the FATO block.
        output += b"OTAF"

        otaf_length = 12 + (4 * num_elements)
        output += otaf_length.to_bytes(4,'little')

        output += num_elements.to_bytes(2,'little')
        output += b"\xFF\xFF"

        # Now for... uh... printing multiples of 0x10000000.

        for i in range(num_elements):
            output += (16*i).to_bytes(4,'little')


        # FATO block done!
        # Next is the FATB block.  
        output += b"BTAF"

        btaf_length = 12 + (16 * num_elements)
        output += btaf_length.to_bytes(4,'little')

        output += num_elements.to_bytes(4,'little')

        # Now we print all the start/end offsets.

        for i in range(num_elements):
            output += b"\x01\x00\x00\x00"   
            output += start_offsets[i].to_bytes(4,'little')  
            output += end_offsets[i].to_bytes(4,'little')   
            output += file_lengths[i].to_bytes(4,'little')


        # FATB block done!
        # Finally the FIMB block, and all the actual data.
 
        output += b"BMIF"
        output += b"\x0C\x00\x00\x00"
        output += end_offsets[-1].to_bytes(4,'little')  

        for file in self.file_list:
            output += file

        return output
        
