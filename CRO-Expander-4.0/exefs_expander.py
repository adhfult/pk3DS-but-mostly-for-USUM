from cro_expander import *

def expand_code(target_code_file, target_header_file, target_NCCH_file, section_to_expand, bytes_to_add, outstring, insertion_point = 0):
	output_code_file = []

	#print(section_to_expand)

	#only need to do all of this if we are updating .code sine
		
	#get the address of end of code. Note that end-offset is actually the address of the first byte of the NEXT thing
	if(section_to_expand in {'c', 'a', 'd'}):

		#insert new bytes for .code
		if(section_to_expand in {'c', 'd'}):
			
			#if code, insert new code in the "text" at the very end. Need to add the offset to the size, otherwise already defined this
			if(insertion_point == 0):

				if(section_to_expand == 'c'):
					#start + 4096*physical region size - loaded offset start 
					insertion_point = hex2dec(target_header_file[0x10:0x14]) + hex2dec(target_header_file[0x14:0x18])*4096 - 0x00100000

					#update size in DecryptedExHeader
					update_offset_pointer(target_header_file, bytes_to_add, 0x18)
					update_offset_pointer(target_header_file, bytes_to_add//4096, 0x14)

				#.data
				else:
					insertion_point = hex2dec(target_header_file[0x30:0x34]) + hex2dec(target_header_file[0x34:0x38])*4096 - 0x00100000
					update_offset_pointer(target_header_file, bytes_to_add, 0x38)
					update_offset_pointer(target_header_file, bytes_to_add//4096, 0x34)

			print('insertion point', dec2hex(insertion_point, 4))

			#grab the portion of the file before insertion
			output_code_file.extend(target_code_file[0:insertion_point])

			#add new bytes
			output_code_file.extend([0xCC]*bytes_to_add)

		else:

			#otherwise was manually defined
			output_code_file.extend(output_code_file[0:insertion_point])
			#outfile now has a copy of the date

			#add new bytes
			output_code_file.extend([0xCC]*bytes_to_add)

		#print(bytes_to_add, insertion_point, skip_check)
		
		#print('Adding', hex(bytes_to_add), 'bytes to', outstring,'at',hex(insertion_point))

		#add the rest of the data
		output_code_file.extend(target_code_file[insertion_point:])


		#Update DectyptedExHeader start offsets, remember that file offsets are 0x100000 less than the encoded adddresses

		#2nd .code
		if(insertion_point + 0x00100000 <= hex2dec(target_header_file[0x20:0x24])):
			update_offset_pointer(target_header_file, bytes_to_add, 0x20)
		#.data
		if(insertion_point + 0x00100000 <= hex2dec(target_header_file[0x30:0x34])):
			update_offset_pointer(target_header_file, bytes_to_add, 0x30)


		#update HeaderNCCH0 sizes and offsets
		#ExeFS size (MU) - in units of 1 media unit = 0x200 = 512 bytes
		target_NCCH_file = update_offset_pointer(target_NCCH_file, bytes_to_add//0x200, 0x1A4)

		#ROMFS offset (MU)
		target_NCCH_file = update_offset_pointer(target_NCCH_file, bytes_to_add//0x200, 0x1B0)

		#now have to update all the pointers, step by 4 since all pointers are 4-aligned

		code_loaded_start = hex2dec(target_header_file[0x20:0x24])

		data_loaded_start = hex2dec(target_header_file[0x30:0x34])
		
		file_loaded_end = data_loaded_start + 4096*hex2dec(target_header_file[0x34:0x38])

		code_file_start = code_loaded_start - 0x00100000


		file_file_end = file_loaded_end - 0x00100000


		if(section_to_expand in {'c'}):

			for offset in range(0, len(output_code_file), 4):

				#in .txt, update anything that looks like a pointer (greater than 0x0100000 and two high bytes empty)
				#in .code, only things that have a pointer pointing to them (check as we go in .txt)
				#in .data, anything that looks like a pointer again

				#determine loader function
				#ST UV WX YZ
				#ST 0V is little-endian offset
				#X = 0xF if is pc

				#W = 9 and Z = 5 is ldr
				#W = D and Z = 1 is ldrh
				#W = D and Z = 5 is ldrh
				#W = 9/B and Z = 8 is ldm(ia) (in this case, the number of 1 bits in STUV is the number of words loaded)

				#byte_0 = output_code_file[offset + 0]
				#byte_1 = output_code_file[offset + 1]

				#temp_W = output_code_file[offset + 2] & 0xF0
				#temp_Z = output_code_file[offset + 3] & 0x0F

				temp_value = hex2dec(output_code_file[offset:offset+4])
				#check if potential pointer points to before start of code.bin or to before the insertion point or after the file end, if so don't do anything
				if(temp_value < 0x00100000 or temp_value < insertion_point + 0x00100000 or temp_value > file_loaded_end):
					continue
				#all pointers point to a multiple of 4gg
				elif(temp_value % 4 != 0):
					continue
				
				#determine what section we are in in new file
				#in .txt
				if (offset < code_file_start):
					#just update, probably no concern
					output_code_file = update_offset_pointer(output_code_file, bytes_to_add, offset)
			
				#in .code or .data, have to be careful to only update real pointers
				elif(offset < file_file_end):
					#look at where the pointer points, see if it's a push to try to identify real pointers (assuming nothing here points to another table)

					#X = 0xD and Z = 5 or 9
					if((output_code_file[temp_value+2 - 0x00100000] & 0x0F == 0xD) and (output_code_file[temp_value+3 - 0x00100000] & 0x0F in {0x5, 0x9})):
						#if passed, the possible pointer is pointing at a push function, so probably valid
						output_code_file = update_offset_pointer(output_code_file, bytes_to_add, offset)
				#print(f'Offset at {dec2hex(offset, 8)}, value {dec2hex(temp_value, 8)}, new value {dec2hex(hex2dec(output_code_file[offset:offset+4]), 8)}')


	#otherwise if we are updating just .bss
	else:
		target_header_file = update_offset_pointer(target_header_file, bytes_to_add, 0x3C)


	match section_to_expand:
		case 'c':
			print('Added', hex(bytes_to_add), 'bytes to', outstring, 'which is', hex(bytes_to_add//4), 'instructions, starting at address', hex(insertion_point), '\n\n')
		case 'd':
			print('Added', hex(bytes_to_add), 'bytes to', outstring, 'starting at address', hex(insertion_point), '\n\n')
		case 'b':
			print('Added', hex(bytes_to_add), 'bytes to', outstring, '\n\n')
	
	return(output_code_file, target_header_file, target_NCCH_file)

def code_expansion_user_input(target_code_file, target_header_file, target_NCCH_file, section_to_expand = '', pages_to_add = 0):


	while True:
		#temp until fix code
		break
		try:
			if(section_to_expand == ''):
				section_to_expand = input('Expand .code, .data, .bss? (c/d/b):\n').lower()
			if(section_to_expand in {'c','d','b'}):
				break
			else:
				print(section_to_expand, 'is not a valid selection.')
				section_to_expand = ''
		except:
			print(section_to_expand, 'is not understood.')
	section_to_expand = 'd'
	match section_to_expand:
		case 'c':
			outstring = '.code'
		case 'd':
			outstring = '.data'
		case 'b':
			outstring = '.bss'
		case _:
			outstring = 'error'

					
	if(section_to_expand in {'c','d', 'r'}):
		print('You can only add space in pages, multiples of 0x1000 bytes')
		while True:
			try:
				if(pages_to_add == 0):
					pages_to_add = int(input('Enter number of pages to add:\n'))
				break
			except:
				print(pages_to_add, 'is not an integer.')
		bytes_to_add = pages_to_add*0x1000
	else:
		while True:
			try:
				pages_to_add = int(input('Enter number of words to add:\n'))
				bytes_to_add = pages_to_add*0x4
				break
			except:
				print(pages_to_add, 'is not an integer.')

				
	return(expand_code(target_code_file, target_header_file, target_NCCH_file, section_to_expand, bytes_to_add, outstring))

def main():
	
	load_new_file = True

	exit_next = False

	target_code_file = []
	target_header_file = []
	target_NCCH_file = []

	output_code_file = []
	output_header_file = []
	output_NCCH_file = []

	while True:
		if(load_new_file):
			target_code_file = load_file('Select code.bin')
			target_header_file = load_file('Select DecryptedExHeader.bin/exheader.bin')
			target_NCCH_file = load_file('Select HeaderNCCH0.bin')
		else:
			target_code_file = output_code_file
			target_header_file = output_header_file
			target_NCCH_file = output_NCCH_file
		process_to_execute = 's'
		#while True:
		#	try:
		#		process_to_execute = input('Expand code.bin segment, move a table, or repoint a function: (s/t/f)\n').lower()
		#		if(process_to_execute in {'s','t','f'}):
		#			break
		#		else:
		#			print(process_to_execute, 'is not a valid selection.')
		#	except:
		#		print(process_to_execute, 'is not understood.')

		if(process_to_execute == 's'):
			output_code_file, output_header_file, output_NCCH_file = code_expansion_user_input(target_code_file, target_header_file, target_NCCH_file)
		#otherwise something in patch table
		#else:
			#output_file = repoint_expand(target_file, process_to_execute)





		while True:
			again_bool = input('Continue Editing?\nY = Continue Editing\nN = Save & Exit Program\n').lower()
			if(again_bool == 'y'):
				load_new_file = False
				target_code_file = output_code_file
				target_header_file = output_header_file
				target_NCCH_file = output_NCCH_file
				break
			elif(again_bool == 'n'):
				exit_next = True
				save_file(output_code_file, asksaveasfilename(title = 'Select output code.bin file'))
				save_file(output_header_file, asksaveasfilename(title = 'Select output DecryptedExHeader.bin/exheader.bin file'))
				save_file(output_NCCH_file, asksaveasfilename(title = 'Select output HeaderNCCH0.bin file'))
				break

		if(exit_next):
			break

	return(True)


if __name__ == "__main__":
    main()