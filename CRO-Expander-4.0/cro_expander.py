from tkinter.filedialog import askopenfilename, asksaveasfilename
import os
#import hashlib

def load_file(title_text):
	search_array = []
	#file we are looking in
	source_file = askopenfilename(title = title_text)
	try:
		with open(source_file, "r+b") as f:
			f.seek(0, os.SEEK_END)
			file_end = f.tell()
			f.seek(0, 0)
			block = f.read(file_end)
		
			for ch in block:
				search_array.append(ch)
			return(search_array)
	except Exception as e:
		print('Encountered following error when opening file ', source_file, '\n', e)
		return([], [], [], [])
    
def save_file(data, path):
	with open(path, "w+b") as f:
		f.write(bytes(data))

def dec2hex(dec_array, padding):
	temp_str = ''
	try:
		for x in dec_array:
			temp_str += str(hex(x)[2:]).zfill(padding).upper()
		return(temp_str)
	except:
		return(str(hex(dec_array)[2:]).zfill(padding).upper())

def hex2dec(hex_array):
	temp = 0
	for offset, byte_digits in enumerate(hex_array):
		#assume little endian, each further offset is multiplied by 256 again
		temp += byte_digits*(256**offset)
	return(temp)

def write_dec_to_bytes(decimals, data, start, length = 4):
	temp = decimals
	for offset in range(length):
		#write the current lowest byte to appropriate offset
		data[start + offset] = decimals & 255
		#bitshift one byte to the right
		decimals = decimals >> 8

	#handle overflowing specified number of bytes
	if(decimals != 0):
		print('Warning, remainder of', decimals, 'left after writing', length, 'bytes', 'at address', hex(start), '. Original value is dec ', temp, '/', hex(dec2hex(temp,6)))
		while True:
			proceed_bool = input('Proceed anyway?\nY/N\n').lower()
			if(proceed_bool == 'y'):
				break
			elif(proceed_bool == 'n'):
				print('Exiting program without writing')
				return(0)
			else:
				print('Please enter Y or N\n')
	return(data)


def update_offset_pointer(data, change, pointer_location, old_code_segment_end = 0x0, pointer_length = 4, ignore_zero_pointer = True, skip_value = 0):
	
	temp = hex2dec(data[pointer_location:pointer_location + pointer_length])

	#if we inserted after this value, don't update it
	if(temp < skip_value):
		return(data)

	#print(temp, change, pointer_location, pointer_length)

	#ignore addresses that are zero or are before the inserted space
	if((ignore_zero_pointer and temp == 0) or temp < old_code_segment_end):

		return(data)
	else:
		return(write_dec_to_bytes(temp + change, data, pointer_location, pointer_length))



def expand_cro(target_file, section_to_expand, bytes_to_add, outstring, file_size, insertion_point = 0):
	output_file = []

	#print(section_to_expand)

	#only need to do all of this if we are updating .code sine
		
	#get the address of end of code. Note that end-offset is actually the address of the first byte of the NEXT thing
	if(section_to_expand in {'c', 'a', 'r'}):
		skip_check = 0
		#insert new bytes for .code
		if(section_to_expand == 'c'):
			
			#if code, insert new code in the "text" at the very end. Need to add the offset to the size, otherwise already defined this
			if(insertion_point == 0):
				insertion_point = hex2dec(target_file[hex2dec(target_file[0xC8:0xCC]):hex2dec(target_file[0xC8:0xCC]) + 0x4]) + hex2dec(target_file[hex2dec(target_file[0xC8:0xCC]) + 0x4:hex2dec(target_file[0xC8:0xCC]) + 0x4 + 0x4])
			#grab the portion of the file before insertion
			output_file.extend(target_file[0:insertion_point])

			#add new bytes
			output_file.extend([0xCC]*bytes_to_add)

			skip_check = 0
		else:
			
			if(section_to_expand == 'r'):
				#inserting at address of .data, which immediately follows relocation patch table
				segment_table_offset = hex2dec(target_file[0xC8:0xCC])


				#patch table end = data start
				insertion_point = hex2dec(target_file[segment_table_offset + 0x18 : segment_table_offset + 0x18 + 4])

			#otherwise was manually defined
			output_file.extend(target_file[0:insertion_point])
			#outfile now has a copy of the date

			#add new bytes
			output_file.extend([0xCC]*bytes_to_add)

			skip_check = insertion_point
		#print(bytes_to_add, insertion_point, skip_check)
		
		print('Adding', hex(bytes_to_add), 'bytes to', outstring,'at',hex(insertion_point))

		#add the rest of the data
		output_file.extend(target_file[insertion_point:])

		#update header file, move from start to end

		#name offset
		output_file = update_offset_pointer(output_file, bytes_to_add, 0x84, insertion_point, skip_value = skip_check)

		#new file size
		file_size += bytes_to_add
		write_dec_to_bytes(file_size, output_file, 0x90)

		#new code size
		if(section_to_expand in {'c'}):
			output_file = update_offset_pointer(output_file, bytes_to_add, 0xB4)

		#if expanding code or relocation patch table, need to advance .data start
		if(section_to_expand in {'c','r'}):
				output_file = update_offset_pointer(output_file, bytes_to_add, 0xB8, insertion_point, skip_value = skip_check)


		#these only need to be repointed if expanding code
		if(section_to_expand in {'c'}):
			#get the other 15 offsets, 4 bytes every 8 bytes from 0xBC
			for x in range(15):
				#x*8 because we are skipping over the size of each, since those are not changing
				output_file = update_offset_pointer(output_file, bytes_to_add, 0xC0 + x*8, insertion_point, skip_value = skip_check)

		#deal with various tables of pointers
		#point to table, table of pointer locations per entry, entry size
		update_pointer_tables_arr = [[0xD0, [0x0], 0x8], [0xF0, [0x0,0x4,0xC], 0x14], [0x100, [0x0, 0x4], 0x8], [0x110, [0x4], 0x8]]

		for table in update_pointer_tables_arr:
			#load the table's pointer
			pointer_pointer = hex2dec(output_file[table[0]:table[0] + 0x4])
			#number of entries
			entry_count = hex2dec(output_file[table[0] + 4:table[0] + 4 + 4])
			#print(pointer_pointer, entry_count)
			for pointer_number in range(entry_count):
				for sub_offset in table[1]:
					output_file = update_offset_pointer(output_file, bytes_to_add, pointer_number*table[2] + sub_offset + pointer_pointer, insertion_point, skip_value = skip_check)
		

		#segment table needed to update a single additional address
		segment_table_offset = hex2dec(output_file[0xC8:0xCC])

		#first update the size of the text table
		if(section_to_expand == 'c'):
			output_file = update_offset_pointer(output_file, bytes_to_add, segment_table_offset + 0x4)
		#size is 0xC, need to go update the start offsets of the 2nd and 3rd entry

		#ROdata
		segment_table_offset += 0xC
		output_file = update_offset_pointer(output_file, bytes_to_add, segment_table_offset, insertion_point, skip_value = skip_check)

		#.data
		segment_table_offset += 0xC
		output_file = update_offset_pointer(output_file, bytes_to_add, segment_table_offset, insertion_point - 1, skip_value = skip_check - 1)


	#otherwise if we are updating just .data or .bss
	else:
		#no edits internally
		output_file = target_file.copy()
		segment_table_offset = hex2dec(output_file[0xC8:0xCC])


		#updating .data
		if(section_to_expand == 'd'):
				
			#check for unused padding at the end of .data

			#total .cro file length - (offset of .data + len(.data)). any extra space is the .data padding to get to a multiple of 0x1000
			free_padding_bytes = hex2dec(output_file[0x90:0x94]) - (hex2dec(output_file[segment_table_offset + 0x18:segment_table_offset + 0x1C]) + hex2dec(output_file[segment_table_offset + 0x1C:segment_table_offset + 0x1C + 0x4]))

			#update total file size less free padding bytes
			output_file = update_offset_pointer(output_file, bytes_to_add, 0x90)

			#update .data size in header
			output_file = update_offset_pointer(output_file, bytes_to_add + free_padding_bytes, 0xBC)

			#update .data size in segment table
			#0x18 is start of .data, +0x4 to its length
			output_file = update_offset_pointer(output_file, bytes_to_add + free_padding_bytes, segment_table_offset + 0x1C)

			#extend the file
			output_file.extend([0xCC]*(bytes_to_add))


		#otherwise expanding .bss
		else:
			#header .bss size
			output_file = update_offset_pointer(output_file, bytes_to_add, 0x94)
			#segment table .bss size
			output_file = update_offset_pointer(output_file, bytes_to_add, segment_table_offset + 0x28)
				
	match section_to_expand:
		case 'c':
			print('Added', hex(bytes_to_add), 'bytes to', outstring, 'which is', hex(bytes_to_add//4), 'instructions, starting at address', hex(insertion_point), '\n\n')
		case 'd':
			print('Added', hex(bytes_to_add), 'bytes to', outstring, 'starting at address', hex(hex2dec(output_file[0x90:0x94]) - bytes_to_add), '\n\n')
		case 'b':
			print('Added', hex(bytes_to_add), 'bytes to', outstring, '\n\n')
	
	return(output_file)

def cro_expansion_user_input(target_file, section_to_expand = '', pages_to_add = 0):
	
	file_size = len(target_file)


	while True:
		try:
			if(section_to_expand == ''):
				section_to_expand = input('Expand .code, relocation patch table, .data, .bss? (c/r/d/b):\n').lower()
			if(section_to_expand in {'c','d','b', 'r'}):
				break
			else:
				print(section_to_expand, 'is not a valid selection.')
				section_to_expand = ''
		except:
			print(section_to_expand, 'is not understood.')
		
	match section_to_expand:
		case 'c':
			outstring = '.code'
		case 'd':
			outstring = '.data'
		case 'b':
			outstring = '.bss'
		case 'r':
			outstring = 'relocation patch table'
		case _:
			outstring = 'error'

	insertion_point = 0

	if(section_to_expand == 'a'):
		while True:
			try:
				insertion_point = input('Enter address to insert at (value at that address will be pushed forwards):\n')

				try:
					insertion_point = int(insertion_point)
				except:
					insertion_point = int(insertion_point, 16)

				print('Inserting at', hex(insertion_point),'\n')
				break
			except:
				print(insertion_point, 'is not an integer.')


					
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

				
	return(expand_cro(target_file, section_to_expand, bytes_to_add, outstring, file_size, insertion_point = 0))



def repoint_expand(target_file, process_to_execute, find_method = '', find_value = 0, update_value = 0):
	
	file_size = len(target_file)

	output_file = target_file.copy()

	segment_table_offset = hex2dec(target_file[0xC8:0xCC])

	code_start = hex2dec(target_file[segment_table_offset:segment_table_offset + 4])
	rodata_start = hex2dec(target_file[segment_table_offset + 0xC:segment_table_offset + 0xC + 4])
	data_start = hex2dec(target_file[segment_table_offset + 0xC + 0xC:segment_table_offset + 0xC + 0xC + 4])
	bss_start = hex2dec(target_file[segment_table_offset + 0xC + 0xC + 0xC:segment_table_offset + 0xC + 0xC + 0xC + 4])

	code_len = hex2dec(target_file[segment_table_offset + 4:segment_table_offset + 4 + 4])
	data_len = hex2dec(target_file[segment_table_offset + 0xC + 0xC + 4:segment_table_offset + 0xC + 0xC + 4 + 4])

	start_table = [code_start, rodata_start, data_start, bss_start]

	patch_table_offset = hex2dec(target_file[0x128:0x12C])
	patch_table_item_count = hex2dec(target_file[0x12C:0x130])
			
	find_value = 0

	target_segment = 0
	target_addend = 0

	table_length = 0

	#we can find the thing we are repointing or expanding either by searching by an address it's written to, or by the value it points to once written (e.g. a function by either the place where its pointer is written, or by the actual address the pointer points to)

	while True:
		try:
			if(find_method == ''):
				find_method = input('Search by either a location where the table/function address is written TO, or by the actual address of the table/function (w/a) :\n').lower()
			if(find_method in {'w','a'}):
				break
			else:
				print(find_method, 'is not a valid selection.')
		except:
			print(find_method, 'is not understood.')

	while True:
		try:
			if(find_value != 0):
				pass
			elif(find_method == 'w'):
				find_value = input('Enter an address where the pointer to your table/function is written to:\n')
			elif(find_method == 'a'):
				find_value = input('Enter the address of your table/function:\n')
			try:
				find_value = int(find_value)
			except:
				find_value = int(find_value, 16)

			print('Looking for', hex(find_value),'\n')
			break
		except:
			print(find_value, 'is not an integer.')

	#look for our function
	if(find_method == 'w'):
		for line in range(patch_table_item_count):
			line_thing = target_file[line*0xC + patch_table_offset:line*0xC + patch_table_offset + 0xC]
			temp = hex2dec(line_thing[0:4])

			#low 4 bits are the segment table id, the rest needs to be bitshifted to the right to get correct offset in that segment
			temp_address = (temp >> 4) + start_table[temp & 0xF]

			if(temp_address == find_value):
				target_segment = line_thing[0x5]
				target_addend = hex2dec(line_thing[0x8:0xC])
				break
	#method a, we need to find the entry where addend + segment_start == entry
	elif(find_method == 'a'):
		for line in range(patch_table_item_count):
			line_thing = target_file[line*0xC + patch_table_offset:line*0xC + patch_table_offset + 0xC]
			temp = hex2dec(line_thing[0x8:0xC])

			temp_address = temp + start_table[line_thing[0x5]]

			if(temp_address == find_value):
				target_segment = line_thing[0x5]
				target_addend = hex2dec(line_thing[0x8:0xC])
				break

	if(target_addend == 0):
		print('Error, no value found')
		return(output_file)

	while True:
		try:
			if(update_value != 0):
				pass
			elif(process_to_execute == 't'):
				update_value = input('Enter the absolute address to which you want the table to start at:\n(You can select an address past the current end of the file, the file will be expanded to make room)\n')
			elif(process_to_execute == 'f'):
				update_value = input('Enter the absolute address to which you want to repoint the function:\n(Note that if you place a function outside of the .code/.text section it will crash if not on Citra)\n')
			try:
				update_value = int(update_value)
			except:
				update_value = int(update_value, 16)

			print('Using', hex(update_value),'\n')

			#if(process_to_execute == 't' and rodata_start > update_value):
			#	print('Selected move-to address is not in .data. Moving Table to a non-.data location is not supported. Please select a location that is at least',start_table[data_start],', and a value that is at least',data_start + data_len,'is recommended unless you have expanded the .data section already and know that the target region is unused.\n')
			#else:
			break
		except Exception as e:
			print(update_value, 'is not an integer.')
			print(e, process_to_execute, rodata_start, update_value)


	
	#table move case
	if(process_to_execute == 't'):
		#update_value = new start of table
		#ask user for length of table
		while True:
			try:
				table_length = input('Enter the current length of the table in bytes:\n')
				try:
					table_length = int(table_length)
				except:
					table_length = int(table_length, 16)

				print('Using', hex(table_length),'\n')
				break
			except:
				print(table_length, 'is not an integer.')

				
		temp = table_length + update_value
		good_length = 0



		#first see if target space exists, if not expand table
		while True:
				

			#if table end is past end of file, expand it
			if(temp >= file_size):
				output_file = expand_cro(output_file, 'd', 0x1000, '.data', len(output_file))

				#update various values
				file_size = len(output_file)

				data_start = hex2dec(output_file[segment_table_offset + 0xC + 0xC:segment_table_offset + 0xC + 0xC + 4])

				data_len = hex2dec(output_file[segment_table_offset + 0xC + 0xC + 4:segment_table_offset + 0xC + 0xC + 4 + 4])

				start_table = [code_start, rodata_start, data_start, bss_start]

				#len_table = [code_len, rodata_len, data_len, bss_len]
			#good_length == table_length if the below passed on the previous loop
			elif(good_length != table_length):
				#check if the data in paste-range in unused (it will be all 0xCC or 0xFF, the empty 0x00 often is spaces intended to be written to after loading)
				#if we have reached this point, we *shouldn't* have out-of-index errors...
					
				temp_cur = update_value
				good_length = 0
				while True:

					#if we have reached the end of the file, break out of this loop with update_value set to file_size, we will place this table at the end and expand .data accordingly
					if(temp_cur > file_size):
						update_value = file_size
						print('No space that looks unused found before end of file, adding additional room at end of file\n')
						break

					#value is a valid empty,
					elif(output_file[temp_cur] in {0xCC, 0xFF}):

						#move table forward to new good section
						if(good_length == 0):
							if(update_value != temp_cur):
								print('Destination looks like it might be used (bytes other than 0xCC or 0xFF detected), attempting table start at', hex(update_value),'\n')
								update_value = temp_cur
						good_length += 1
					else:
						good_length = 0
					#table fits in location
					if(good_length == table_length):
						#just make sure we have the .data expanded in case we are in the padding region
						if(data_start + data_len < file_size):
							#need to update data size in header plus the segment table
							output_file = write_dec_to_bytes(file_size - data_start, output_file, 0xBC, length = 4)
							output_file = write_dec_to_bytes(file_size - data_start, output_file, (hex2dec(output_file[0xC8:0xCC]) + 2*0xC + 0x4), length = 4)


						break
			else:
				break

		#update_value is now valid start of a place we can fit the table. Just need to copy over the table, then repoint references.
		old_table_absolute = target_addend + start_table[target_segment]
		
		update_segment = 0
		#get segment of target location
		if(update_value > data_start + data_len):
			update_segment = 3
		elif(update_value < code_start + code_len):
			update_segment = 0
		elif(update_value < data_start):
			update_segment = 1
		else:
				update_segment = 2

		#write values to new location
		for ind in range(table_length):
			output_file[update_value + ind] = output_file[old_table_absolute + ind]
			#set old space to 0xCC
			output_file[old_table_absolute + ind] = 0xCC

		
		print('\nTable now starts at', hex(update_value),'segment offset: ',hex(start_table[update_segment]),'\n')
		
		#finally, look for everywhere in relocation patches that either writes a pointer TO the table,or writes a pointer IN the table, and update them
		for line in range(patch_table_item_count):
			#get the line

			line_thing = output_file[line*0xC + patch_table_offset:line*0xC + patch_table_offset + 0xC]

			temp = hex2dec(line_thing[0:4])
			
			#location pointer is written to
			write_offset = (temp >> 4)

			#location pointer is pointing at
			pointed_at = hex2dec(line_thing[0x8:0xC])

			#check if points AT an address in our table
			if(target_addend <= pointed_at < target_addend + table_length and line_thing[0x5] == target_segment):
				
				#set new segment
				output_file[line*0xC + patch_table_offset + 0x5] = update_segment

				#offset into .data
				output_file = write_dec_to_bytes(update_value - start_table[update_segment] + (pointed_at - target_addend), output_file, line*0xC + patch_table_offset + 0x8, length = 4)
				print('Updated reference to table now at', hex(update_value - start_table[update_segment] + (pointed_at - target_addend)),'\tPatch Table entry #',line,'\taddress:',hex(line*0xC + patch_table_offset))

			#writes TO an address in our table
			if(target_addend <= write_offset < target_addend + table_length):

				#relative to start of old table offset = (write_offset - target_addend)
				#absolute address it should be written to = update_value + (write_offset - target_addend)
				#relative to segment = ((update_value + (write_offset - target_addend)) - start_table[update_segment])

				output_file = write_dec_to_bytes((((update_value + (write_offset - target_addend)) - start_table[update_segment]) << 4) + update_segment, output_file, line*0xC + patch_table_offset)
				print('Updated pointer in table now at', hex(update_value + (write_offset - target_addend)),'\tPatch Table entry #',line,'\taddress:',hex(line*0xC + patch_table_offset))

	else:
	#Function case
		for line in range(patch_table_item_count):
			line_thing = output_file[line*0xC + patch_table_offset:line*0xC + patch_table_offset + 0xC]
			#found instance
			if(hex2dec(line_thing[0x8:0xC]) == target_addend and line_thing[0x5] == target_segment):
				output_file = write_dec_to_bytes(update_value - start_table[output_file[line*0xC + patch_table_offset + 0x5]], output_file, line*0xC + patch_table_offset + 8, length = 4)
				temp = hex2dec(line_thing[0:4])
				print('Updated function call/pointer at', hex((temp >> 4) + start_table[temp & 0xF]),'\nPatch Table entry #',line,'address:',hex(line*0xC + patch_table_offset))

	return(output_file)



def main():
	
	load_new_file = True
	save = True
	exit_next = False
	target_file = []
	output_file = []
	while True:
		if(load_new_file):
			target_file = load_file('Select cro file')
		else:
			target_file = output_file
		process_to_execute = ''
		while True:
			try:
				process_to_execute = input('Expand .cro segment, move a table, or repoint a function: (s/t/f)\n').lower()
				if(process_to_execute in {'s','t','f'}):
					break
				else:
					print(process_to_execute, 'is not a valid selection.')
			except:
				print(process_to_execute, 'is not understood.')

		if(process_to_execute == 's'):
			output_file = cro_expansion_user_input(target_file)
		#otherwise something in patch table
		else:
			output_file = repoint_expand(target_file, process_to_execute)





		while True:
			again_bool = input('Continue Editing?\nY = Continue Editing Current CRO\nS = Save & Select Another CRO\nN = Save & Exit Program\n').lower()
			if(again_bool == 'y'):
				load_new_file = False
				save = False
				break
			elif(again_bool == 's'):
				load_new_file = True
				save = True
				break
			elif(again_bool == 'n'):
				save = True
				exit_next = True
				break

		if(save):
			output_file_path = asksaveasfilename(title = 'Select output cro file')
			save_file(output_file, output_file_path)

		if(exit_next):
			break

	return(True)

if __name__ == "__main__":
    main()