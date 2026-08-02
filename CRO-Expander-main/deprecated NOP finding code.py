def compute_branch_instruction(dest, current):

	#(dest - Current)>>2 - 2 OR (dest - PC)>>2 - 1
	instruction_offset = (dest - current)//4 - 2

	#need to convert to 24-bit signed, add 2^24 if less than 0
	if(instruction_offset < 0):
		instruction_offset += 2^24

	return(instruction_offset)

def nop_search(data, offset, file_length, min_length_needed = 3):

	number_in_a_row = 0
	previous_line_was_branch = False

	if(min_length_needed == -1):
		total_useable_nops = 0
		while True:
			if(data[offset:offset + 4] == [0x00, 0xF0, 0x20, 0xE3]):
				number_in_a_row += 1
				#if this is the first one, check to see if previous line is a branch instruction. If it is, we don't need to do a branch over our code
				if(number_in_a_row == 1):
					#branch instruction has lower half-byte 1, A, or B (bx, b, bl)
					if((data[offset - 1] & 0x0F) in {0x0A, 0x0B, 0x01}):
						previous_line_was_branch = True
			#if it's not a nop but we've found more than the minimum needed number in a row, return the offset and number found
			elif(number_in_a_row >= 2):
				#the number of useable spots is one less than the number in a row, since the last one needs to be for a branch
				total_useable_nops += number_in_a_row - 1
				number_in_a_row = 0
				#if this is false, need an extra nop for the passover branch
				if(not(previous_line_was_branch)):
					total_useable_nops -= 1
				#otherwise we have an extra nop and we need to reset it to false
				else:
					previous_line_was_branch = False
			offset += 4
			if(offset >= file_length):
				return(total_useable_nops)
				
	else:
		while True:
			#if we've found a nop, increment count of nops in a row found
			if(data[offset:offset + 4] == [0x00, 0xF0, 0x20, 0xE3]):
				number_in_a_row += 1
				#branch instruction has lower half-byte 1, A, or B (bx, b, bl)
				if((data[offset - 1] & 0x0F) in {0x0A, 0x0B, 0x01}):
						previous_line_was_branch = True


			#if it's not a nop but we've found more than the minimum needed number in a row, return the offset and number found. If previous line to the nop block was a branch, we have an extra nop
			elif(number_in_a_row >= min_length_needed or (number_in_a_row >= (min_length_needed - 1) and previous_line_was_branch)):
				return(offset - number_in_a_row*4, number_in_a_row, previous_line_was_branch)
			#otherwise, reset the count.
			else:
				number_in_a_row = 0
				previous_line_was_branch = False

			offset += 4
			#increment offset by 1 (next instruction)
			if(offset >= file_length):
				return(file_length, 0)


def nop_inserter_main():
	
	#code.bin or .cro file
	target_file = load_file('Select .bin or cro file to insert code into')

	

	#file with assembly code
	code_file = load_file('Select binary file containing code to insert. Multiple Functions should be seperated by terminator 0xFF FF FF FF FF')

	#make array to hold functions
	function_array = []
	temp = []

	code_file_length = len(code_file)

	target_file_length = len(target_file)

	#avoid too small code error
	if(code_file_length < 9):
		print('Code file too small to make sense')

	try:
		#find individual functions
		offset = 0
		while True:

			#reached a terminator, 
			if(code_file[offset:offset+5] == [0xFF, 0xFF, 0xFF, 0xFF, 0xFF]):
				function_array.append(temp)
				temp = []
				offset += 5
			#grab next instruction
			else:
				temp = [*temp, *code_file[offset:offset+4]]
				offset += 4
				
			#reached end of code
			if(offset >= code_file_length):
				#catch the case of not putting a terminator after last function
				if(temp != []):
					temp = [*temp, *code_file[offset:offset+4]]
					function_array.append(temp)
				break

	except Exception as e:
		print('Error occured parsing code file')
		print(e)

	bytecount_mismatch = False

	for rownumber, row in enumerate(function_array):
		print('Function ' + str(rownumber))
		
		if(len(row) % 4 != 0):
			print(len(row)/4, 'instructions,', len(row), 'bytes')
			print('Warning, something is wrong with this function! Number of bytes 0 mod 4.')
			bytecount_mismatch = True
		else:
			print(len(row)//4, 'instructions,', len(row), 'bytes')
		print('\n')
	
	if(bytecount_mismatch):
		temp = input('')
		return

	master_offset = 0
	address_book = []
	new_function_array = []

	#iterate over the functions
	for function_number, functions in enumerate(function_array):
		offset = master_offset
		#see if we can fit the entire function somewhere
		offset, nop_count, no_passover = nop_search(target_file, offset, target_file_length, len(functions)//4)
		#if so, entire thing goes here
		if(offset < target_file_length):
			address_book.append([[offset, nop_count, no_passover]])
		else:
			#reset offset to previous position
			offset = master_offset
			instructions_needed = len(functions)//4
			temp_array = []
			while True:
				#find next nop at-least-pair
				offset, nop_count, no_passover = nop_search(target_file, offset, target_file_length, 3)

				#if nop_count is less, will write as many instructions as possible then a branch without link instruction in last spot. If equal, the bx lr will go in last spot. if greater, save for later use
				if(nop_count < instructions_needed):
					temp_array.append([offset, nop_count, no_passover])
					#since we need to keep going, one of the nops is replaced by the branch instruction, so we only found room for one fewer than nop_count instructions
					instructions_needed -= (nop_count - 1)
					#print(instructions_needed)
				#done with this function (if nop_count > instructions needed, keep looking because we don't want to waste space)
				elif(nop_count == instructions_needed):
					temp_array.append([offset, nop_count])
					address_book.append(temp_array)
					break
				#increment offset
				offset += 4
				if(offset >= target_file_length):
					print('Could not find room for function', function_number)
					return
		master_offset = offset + 4
		#now we now have an array of addresses and the number of instructions and if we need a passover in front we have at that point at address_book[function_number] for the current function
		#make a new function array. each function subarray is of the form [address, A, B, C, D] where A is at address, B is address + 1, etc.
		nop_block = 0
		temp_new_function = []
		bytes_assigned = 0
		instructions_needed = len(functions)//4
		while True:
			temp_offset = address_book[function_number][nop_block][0]
			temp_no_passover_bool = address_book[function_number][nop_block][2]
			#continue

			


			#we have X nops, use the first X-1 of them
			for number, address_line in enumerate(range(address_book[function_number][nop_block][1] - 1)):

				#if first instruction in this block and not a passover, need to write a passover branch, otherwise proceed as normal
				if(number == 0 and not temp_no_passover_bool):

					#grab last nop block address, then add 4 times the number of instructions
					passover_destination = address_book[function_number][-1][0] + 4 * address_book[function_number][nop_block][1]

					instruction_offset = compute_branch_instruction(passover_destination, temp_offset)
					temp_new_function.append([temp_offset, (instruction_offset & 0xFF), (instruction_offset>>2) & 0xFF, (instruction_offset>>4) & 0xFF, 0xEA])
				else:
					temp_new_function.append([temp_offset, functions[bytes_assigned], functions[bytes_assigned + 1], functions[bytes_assigned + 2], functions[bytes_assigned + 3]])
					instructions_needed -= 1
					bytes_assigned += 4

				temp_offset += 4
				

			#if we have exactly 1 instruction left, stick it in the last nop. otherwise write another branch
			if(instructions_needed == 1):
				temp_new_function.append([temp_offset, functions[bytes_assigned], functions[bytes_assigned + 1], functions[bytes_assigned + 2], functions[bytes_assigned + 3]])
				#append this to the new function array
				new_function_array.append(temp_new_function)
				#and break the while loop
				break
			#otherwise, we need to make a branch instruction
			else:
				#increment nop block now, makes everything simpler
				nop_block +=1

				instruction_offset = compute_branch_instruction(address_book[function_number][nop_block][0], temp_offset)

				#instruction_offset is 3 bytes, need to & with FF, FF00, and FF000, then rightshift appropriate number of times to make 2 bytes each. last byte is 0xEA for uncondtional simple branch
				temp_new_function.append([temp_offset, (instruction_offset & 0xFF), (instruction_offset>>2) & 0xFF, (instruction_offset>>4) & 0xFF, 0xEA])

			
		
	
	#now print the coming edits. [address, A, B, C, D] where A is at address, B is address + 1, etc.
	#for instruction_line in new_function_array:
	#	for x in instruction_line:
	#		print(x)
		#print(instruction_line[0], str(instruction_line[1]) + str(instruction_line[2]) + str(instruction_line[3]) + str(instruction_line[4]))

	#print edits


	

	#write edits to csv file
	edit_summary_file_path = asksaveasfilename(title = 'Select csv file to save edit summary', defaultextension = '.csv')

	total_nops_used = 0
	with open(edit_summary_file_path, 'w', newline='', encoding='utf-8-sig') as csvfile:
		writer_head = csv.writer(csvfile, dialect='excel', delimiter=',')
		#write the header line
		writer_head.writerow (['Offset', 'Hex'])
		for function_line in new_function_array:
			for instruction_line in function_line:
				writer_head.writerow([dec2hex([instruction_line[0]],8), dec2hex(instruction_line[1:5], 2)])
				total_nops_used += 1

				#while we're writing, we might as well use this same for loop to update the file we have in memory that we're going to write
				for x in range(4):
					target_file[instruction_line[0] + x] = instruction_line[1 + x]

	
	total_nops_left = nop_search(target_file, 0, target_file_length, -1)

	while True:
		go_ahead = input('There are a total of ~' + str(total_nops_left) + ' usable instructions left after this write.\nYou can also check the CSV output before committing.\nContinue? Y/N\n').upper()
		if(go_ahead == 'Y'):
			break
		elif(go_ahead == 'N'):
			return

	output_file_path = asksaveasfilename(title = 'Select output code file')
	save_file(target_file, output_file_path)