import cmdReference
import garc
import sesd

#from jsoncomment import JsonComment
import json
import os
import traceback

#json = JsonComment()



#####################################
#    ACTUAL CONVERSIONS - Switch    #
#####################################


def convert_json_to_bseq(input_ref, json_filename="", output_filename=""):

    game = input_ref.game

    # First, ask for the filename if it wasn't given.
    while not json_filename:
        json_filename = input("Type the filename of the JSON to convert!\n")
        if not os.path.isfile(json_filename):
            print("Hey what? I can't find a file by that name.")
            json_filename = ""
        elif ".json" not in json_filename:
            print("That's definitely not a .json!")
            json_filename = ""

    if not output_filename:
        output_filename = json_filename.replace(".json", ".bseq")


        
    # Now to actually read in the input JSON.

    try:
        with open(json_filename, "r") as infile:
            json_file = json.load(infile)

    except FileNotFoundError:
        error_string = (f"Couldn't find the file {json_filename}.")
        raise FileNotFoundError(error_string)

    except PermissionError:
        error_string = (f"Couldn't access the file {json_filename}.\n"+
                        "Do you have it open in another program?")
        raise PermissionError(error_string)
    
    except json.JSONDecodeError as e:
        error_string = (f"The file is not a valid JSON, for this reason:\n"+
                        e.args[0])
        raise ValueError(error_string)

    sesd_obj = sesd.SESD.from_dict(json_file, game, input_ref)

    # NOW. With sesd_obj as a kinda wrapper for all that data...
    # Let's export it as a BDSP-ready JSON!

    output_bytes = sesd_obj.get_bseq()

    with open(output_filename, "wb") as outfile:
        outfile.write(output_bytes)




def convert_bseq_to_json(input_ref, bdsp_ref, 
                         bseq_filename="", output_filename="",
                         opts_setting=False, indents=2, uabea=False,
                         warns_setting = 2):

    game = input_ref.game

    # First, ask for the filename if it wasn't given.
    while not bseq_filename:
        bseq_filename = input("Type the filename of the BSEQ to convert!\n")
        if not os.path.isfile(bseq_filename):
            print("Hey what? I can't find a file by that name.")
            bseq_filename = ""
        elif ".bseq" not in bseq_filename:
            print("That's definitely not a .bseq!")
            bseq_filename = ""

    if not output_filename:
        output_filename = bseq_filename.replace(".bseq", ".json")


        
    # Now to actually read the input BSEQ.

    try:
        with open(bseq_filename, "rb") as infile:
            bseq_bytes = infile.read()

    except FileNotFoundError:
        error_string = (f"Couldn't find the file {bseq_filename}.")
        raise FileNotFoundError(error_string)

    except PermissionError:
        error_string = (f"Couldn't access the file {bseq_filename}.\n"+
                        "Do you have it open in another program?")
        raise PermissionError(error_string)

    sesd_obj = sesd.SESD.from_bytes(bseq_bytes, game, input_ref)

    # NOW. With sesd_obj as a kinda wrapper for all that data...
    # Let's export it as a JSON!

    seq_name = os.path.split(bseq_filename)[1].split(".bseq")[0]

    if bdsp_ref:
        output_dict = sesd_obj.get_json_dict_bdsp(bdsp_ref, seq_name,
                                                  opts_setting, uabea,
                                                  warns_setting)
    else:
        output_dict = sesd_obj.get_json_dict_simple(seq_name, opts_setting)

    with open(output_filename, "w") as outfile:
        json.dump(output_dict, outfile, indent=indents)




def convert_all_jsons_to_bseq(game):

    attempted = succeeded = 0

    cwd = os.getcwd()
    dict_folder = "commandDictionaries"

    # First, try to read in the command reference JSON.
    input_ref_filename = os.path.join(dict_folder, game+"CommandReference.json")
    input_ref_path = os.path.join(cwd, input_ref_filename)
    
    try:
        with open(input_ref_path, "r") as infile:
            ref_dict = json.load(infile)

    except FileNotFoundError:
        error_string = (f"Couldn't find the file {input_ref_filename}.")
        raise FileNotFoundError(error_string)

    except PermissionError:
        error_string = (f"Couldn't access the file {input_ref_filename}.\n"+
                        "Do you have it open in another program?")
        raise PermissionError(error_string)

    except json.JSONDecodeError as e:
        error_string = (f"{game}CommandReference.json is not in a valid "+
                        "JSON format:\n" + e.args[0])
        raise ValueError(error_string)

    
    input_ref = cmdReference.CmdReference(ref_dict)


    # Now to actually convert everything!
    input_folder = "inputJSONs"
    output_folder = "outputBSEQs"

    input_files_list = list()
    output_files_list = list()
    readable_input_files_list = list()

    input_path = os.path.join(cwd, input_folder)
    output_path = os.path.join(cwd, output_folder)

    # Gonna grab all the input files from the input folder~
    for (dirpath, dirnames, filenames) in os.walk(input_path):
        readable_input_files_list += [file for file in filenames]
        input_files_list += [os.path.join(dirpath, file) for file in filenames]
        output_files_list += [os.path.join(output_path, file)
                              for file in filenames]

    for i, input_filename in enumerate(input_files_list):
        # But let's only look at the ones that are actually .json files.
        if ".json" in input_filename:
            output_filename = output_files_list[i].replace(".json", ".bseq")
            try:
                attempted += 1
                convert_json_to_bseq(input_ref, input_filename, output_filename)
                print("Successfully converted", readable_input_files_list[i],
                      flush=True)
                succeeded += 1
                
            except (ValueError, TypeError,
                    FileNotFoundError, PermissionError) as e:
                print(("*"*30+"\n")*3)
                print("Uh oh, an error occurred when converting the JSON \n\""+
                      readable_input_files_list[i]+"\":\n" + e.args[0],
                      "\n\nAsk Namadu if you don't understand it!")
                print(("*"*30+"\n")*3)
                

                
    print("\n\n*****Files converted!*****")
    print(f"{succeeded} out of {attempted} files successfully converted.")
    if succeeded != attempted:
        print("Please scroll up to find which file(s) didn't work out...")
        print("Mayhap Namadu can help figure out what went wrong?")
        print("Anyway...")
    
    return True




def convert_all_bseqs_to_json(game, opts_setting=1, indents=2,
                              warns_setting=2, bdsp=False, uabea=False):

    attempted = succeeded = 0

    cwd = os.getcwd()
    dict_folder = "commandDictionaries"

    # First, try to read in the command reference JSON.
    input_ref_filename = os.path.join(dict_folder, game+"CommandReference.json")
    input_ref_path = os.path.join(cwd, input_ref_filename)
    
    try:
        with open(input_ref_path, "r") as infile:
            ref_dict = json.load(infile)

    except FileNotFoundError:
        error_string = (f"Couldn't find the file {input_ref_filename}.")
        raise FileNotFoundError(error_string)

    except PermissionError:
        error_string = (f"Couldn't access the file {input_ref_filename}.\n"+
                        "Do you have it open in another program?")
        raise PermissionError(error_string)

    except json.JSONDecodeError as e:
        error_string = (f"{game}CommandReference.json is not in a valid "+
                        "JSON format:\n" + e.args[0])
        raise ValueError(error_string)
                
        

    input_ref = cmdReference.CmdReference(ref_dict)

    # Next, the BDSP command reference JSON, if needed.
    if bdsp:
        
        bdsp_ref_filename = os.path.join(dict_folder,
                                         "BDSPCommandReference.json")
        bdsp_ref_path = os.path.join(cwd, bdsp_ref_filename)
        
        try:
            with open(bdsp_ref_path, "r") as infile:
                bdsp_ref_dict = json.load(infile)
        except FileNotFoundError:
            error_string = (f"Couldn't find the file {bdsp_ref_filename}.")
            raise FileNotFoundError(error_string)

        except PermissionError:
            error_string = (f"Couldn't access the file {bdsp_ref_filename}.\n"+
                            "Do you have it open in another program?")
            raise PermissionError(error_string)
        
        except json.JSONDecodeError as e:
            error_string = (f"BDSPCommandReference.json is not in a valid "+
                            "JSON format:\n" + e.args[0])
            raise ValueError(error_string)

    
        bdsp_ref = cmdReference.CmdReference(bdsp_ref_dict)


    # Now to actually convert everything!
    input_folder = "inputBSEQs"
    output_folder = "outputJSONs"

    input_files_list = list()
    output_files_list = list()
    readable_input_files_list = list()

    input_path = os.path.join(cwd, input_folder)
    output_path = os.path.join(cwd, output_folder)

    # Gonna grab all the input files from the input folder~
    for (dirpath, dirnames, filenames) in os.walk(input_path):
        readable_input_files_list += [file for file in filenames]
        input_files_list += [os.path.join(dirpath, file) for file in filenames]
        output_files_list += [os.path.join(output_path, file)
                              for file in filenames]

    for i, input_filename in enumerate(input_files_list):
        # But let's only look at the ones that are actually .bseq files.
        if ".bseq" in input_filename:
            output_filename = output_files_list[i].replace(".bseq", ".json")
            try:
                attempted += 1
                if bdsp:
                    convert_bseq_to_json(input_ref, bdsp_ref, input_filename,
                                         output_filename,
                                         opts_setting, indents, uabea,
                                         warns_setting)
                else:
                    convert_bseq_to_json(input_ref, False, input_filename,
                                         output_filename,
                                         opts_setting, indents)
                print("Successfully converted", readable_input_files_list[i],
                      flush=True)
                succeeded += 1

                
            except (ValueError, TypeError,
                    FileNotFoundError, PermissionError) as e:
                print(("*"*30+"\n")*3)
                print("Uh oh, an error occurred when converting the BSEQ \n\""+
                      readable_input_files_list[i]+"\":\n" + e.args[0],
                      "\nAsk Namadu if you don't understand it!")
                print(("*"*30+"\n")*3)                
                

                
    print("\n\n*****Files converted!*****")
    print(f"{succeeded} out of {attempted} files successfully converted.")
    if succeeded != attempted:
        print("Please scroll up to find which file(s) didn't work out...")
        print("Mayhap Namadu can help figure out what went wrong?")
        print("Anyway...")

    if bdsp:
        if warns_setting != 0:
            print("Before importing any of these files, please do a Ctrl+F")
            print("through each one for the word WARNING.")
            print("Some commands will need to be replaced or otherwise altered")
            print("to actually work in BDSP.")
            print("Remove the warnings once you've dealt with them!")
        else:
            print("Remember that the sequences most likely won't work")
            print("if you import them to BDSP as-is. They will need to be")
            print("edited by hand.")
    
    return True


#####################################
#      ACTUAL CONVERSIONS - 3DS     #
#####################################


def extract_garc_files(game: str, convert_to_json: bool, opts_setting=1, indents=2,
                              warns_setting=2, bdsp=False, uabea=False):

    attempted = succeeded = 0

    game_garcs = {"XY": "6", "ORAS": "4", "SM": "8", "USUM": "8"}
    
    cwd = os.getcwd()
    garc_folder = "targetGARC"
    json_output_folder = "outputJSONs"
    bseq_output_folder = "outputBSEQs"

    dict_folder = "commandDictionaries"

   


    # First, let's try to actually read the GARC.
    garc_filename = os.path.join(garc_folder, game_garcs[game])
    garc_path = os.path.join(cwd, garc_filename)
    
    try:
        with open(garc_path, "rb") as infile:
            garc_contents = infile.read()

    except FileNotFoundError:
        error_string = (f"Couldn't find the file {garc_filename}.")
        raise FileNotFoundError(error_string)

    except PermissionError:
        error_string = (f"Couldn't access the file {garc_filename}.\n"+
                        "Do you have it open in another program?")
        raise PermissionError(error_string)

    

    try:
        garc_obj = garc.GARC.from_garc_file(garc_contents, game, True)
    except ValueError as e:
        print(("*"*30+"\n")*3)
        print("Uh oh, an error occurred when reading the GARC:\n"+ e.args[0],
              "\nAre you sure you have the right file? If so,",
              "then ask Namadu if you don't understand it!")
        print(("*"*30+"\n")*3)

        

    if convert_to_json:

        # If we're converting, we'll need the command reference JSON.
        input_ref_filename = os.path.join(dict_folder,
                                          game+"CommandReference.json")
        input_ref_path = os.path.join(cwd, input_ref_filename)
       
        try:
            with open(input_ref_path, "r") as infile:
                ref_dict = json.load(infile)

        except FileNotFoundError:
            error_string = (f"Couldn't find the file {input_ref_filename}.")
            raise FileNotFoundError(error_string)

        except PermissionError:
            error_string = (f"Couldn't access the file {input_ref_filename}.\n"+
                            "Do you have it open in another program?")
            raise PermissionError(error_string)

        except json.JSONDecodeError as e:
            error_string = (f"{game}CommandReference.json is not in a valid "+
                            "JSON format:\n" + e.args[0])
            raise ValueError(error_string)

        input_ref = cmdReference.CmdReference(ref_dict)

        # And if we're converting to BDSP JSON format, we need that one too.

        if bdsp:
            
            bdsp_ref_filename = os.path.join(dict_folder,
                                             "BDSPCommandReference.json")
            bdsp_ref_path = os.path.join(cwd, bdsp_ref_filename)
            
            try:
                with open(bdsp_ref_path, "r") as infile:
                    bdsp_ref_dict = json.load(infile)
            except FileNotFoundError:
                error_string = (f"Couldn't find the file {bdsp_ref_filename}.")
                raise FileNotFoundError(error_string)

            except PermissionError:
                error_string = (f"Couldn't access the file {bdsp_ref_filename}.\n"+
                                "Do you have it open in another program?")
                raise PermissionError(error_string)
            
            except json.JSONDecodeError as e:
                error_string = (f"BDSPCommandReference.json is not in a valid "+
                                "JSON format:\n" + e.args[0])
                raise ValueError(error_string)

        
            bdsp_ref = cmdReference.CmdReference(bdsp_ref_dict)

        
        for i,file in enumerate(garc_obj.file_list):
            try:
                attempted += 1
                # First, we'll take a file and parse it as an SESD.
                sesd_obj = sesd.SESD.from_bytes(file, game, input_ref)

                # With sesd_obj as a kinda wrapper for a file's data...
                # Let's export it as a JSON!

                seq_name = str(i)

                if bdsp:
                    output_dict = sesd_obj.get_json_dict_bdsp(bdsp_ref,
                                                              seq_name,
                                                              opts_setting,
                                                              uabea,
                                                              warns_setting)
                else:
                    output_dict = sesd_obj.get_json_dict_simple(seq_name,
                                                                opts_setting)


                output_filename = os.path.join(cwd, json_output_folder,
                                               seq_name + ".json")
                
                with open(output_filename, "w") as outfile:
                    json.dump(output_dict, outfile, indent=indents)
                print("Successfully converted", seq_name + ".json",
                      flush=True)
                succeeded += 1
                
            except (ValueError, TypeError,
                    FileNotFoundError, PermissionError) as e:
                print(("*"*30+"\n")*3)
                print("Uh oh, an error occurred when converting BSEQ #"+
                      str(i)+":\n" + e.args[0],
                      "\nAsk Namadu if you don't understand it!")
                print(("*"*30+"\n")*3)                      

    else:
        # If we're just exporting the files as raw BSEQs,
        # no need to deal with conversion! Yay!
        for i, file in enumerate(garc_obj.file_list):
            attempted += 1

            seq_name = str(i)
            
            output_filename = os.path.join(cwd, bseq_output_folder,
                                           seq_name + ".bseq")
            
            with open(output_filename, "wb") as outfile:
                outfile.write(file)
                print("Successfully exported", seq_name + ".bseq",
                      flush=True)
                succeeded += 1
            

    print("\n\n*****Files exported!*****")
    print(f"{succeeded} out of {attempted} files successfully exported.")
    if succeeded != attempted:
        print("Please scroll up to find which file(s) didn't work out...")
        print("Mayhap Namadu can help figure out what went wrong?")
        print("Anyway...")

    if bdsp:
        if warns_setting != 0:
            print("Before importing any of these files, please do a Ctrl+F")
            print("through each one for the word WARNING.")
            print("Some commands will need to be replaced or otherwise altered")
            print("to actually work in BDSP.")
            print("Remove the warnings once you've dealt with them!")
        else:
            print("Remember that the sequences most likely won't work")
            print("if you import them to BDSP as-is. They will need to be")
            print("edited by hand.")
    
    return True

   



def import_garc_files(game: str, convert_from_json: bool):

    attempted = succeeded = 0
    
    game_garcs = {"XY": "6", "ORAS": "4", "SM": "8", "USUM": "8"}
    
    cwd = os.getcwd()
    garc_folder = "targetGARC"
    json_input_folder = "inputJSONs"
    bseq_input_folder = "inputBSEQs"

    dict_folder = "commandDictionaries"

    # Some info we'll need to know what to import.
    if convert_from_json:
        input_folder = json_input_folder
        extension = ".json"
    else:
        input_folder = bseq_input_folder
        extension = ".bseq"  

    # First, let's try to actually read the GARC.
    garc_filename = os.path.join(garc_folder, game_garcs[game])
    garc_path = os.path.join(cwd, garc_filename)
    
    try:
        with open(garc_path, "rb") as infile:
            garc_contents = infile.read()

    except FileNotFoundError:
        error_string = (f"Couldn't find the file {garc_filename}.")
        raise FileNotFoundError(error_string)

    except PermissionError:
        error_string = (f"Couldn't access the file {garc_filename}.\n"+
                        "Do you have it open in another program?")
        raise PermissionError(error_string)

    

    try:
        garc_obj = garc.GARC.from_garc_file(garc_contents, game, True)
    except ValueError as e:
        print(("*"*30+"\n")*3)
        print("Uh oh, an error occurred when reading the GARC:\n"+ e.args[0],
              "\nAsk Namadu if you don't understand it!")
        print(("*"*30+"\n")*3)


    # Let's see how many files we have to import.

    files_to_import = []





    if convert_from_json:        

        # If we're converting, we'll need the command reference JSON.
        input_ref_filename = os.path.join(dict_folder,
                                          game+"CommandReference.json")
        input_ref_path = os.path.join(cwd, input_ref_filename)
       
        try:
            with open(input_ref_path, "r") as infile:
                ref_dict = json.load(infile)

        except FileNotFoundError:
            error_string = (f"Couldn't find the file {input_ref_filename}.")
            raise FileNotFoundError(error_string)

        except PermissionError:
            error_string = (f"Couldn't access the file {input_ref_filename}.\n"+
                            "Do you have it open in another program?")
            raise PermissionError(error_string)

        except json.JSONDecodeError as e:
            error_string = (f"{game}CommandReference.json is not in a valid "+
                            "JSON format:\n" + e.args[0])
            raise ValueError(error_string)

        input_ref = cmdReference.CmdReference(ref_dict)

    # Okay! Let's actually start looking at the files we have to import.

    for i in range(len(garc_obj.file_list)):
        import_filename = os.path.join(cwd, input_folder, str(i)+extension)
        if os.path.exists(import_filename):

            # Hey, the file exists! Let's read it...
            try:
                attempted += 1
                if convert_from_json:
                    with open(import_filename,"r") as infile:
                        json_dict = json.load(infile)

                    sesd_obj = sesd.SESD.from_dict(json_dict, game, input_ref)
                    sesd_bytes = sesd_obj.get_bseq()

                else:
                    with open(import_filename,"rb") as infile:
                        sesd_bytes = infile.read()

                # ...and replace it in the GARC.

                garc_obj.replace_file(i,sesd_bytes)
                
                print("Successfully imported", str(i)+extension,
                      flush=True)
                succeeded += 1
                
            except (ValueError, TypeError,
                    FileNotFoundError, PermissionError) as e:
                print(("*"*30+"\n")*3)
                print("Uh oh, an error occurred when reading the file \n\""+
                      str(i)+extension+"\":\n" + e.args[0],
                      "\nAsk Namadu if you don't understand it!")
                print(("*"*30+"\n")*3)


    # We've imported all the possible files - now time to write the new GARC.

    garc_bytes = garc_obj.get_garc()

    try:
        with open(garc_path, "wb") as infile:
            infile.write(garc_bytes)
    except FileNotFoundError:
        error_string = (f"Couldn't find the file {garc_filename}.")
        raise FileNotFoundError(error_string)
    except PermissionError:
        error_string = (f"Couldn't access the file {garc_filename}.\n"+
                        "Do you have it open in another program?")
        raise PermissionError(error_string)    
            
    print("\n\n*****Files imported!*****")
    print(f"{succeeded} out of {attempted} files successfully converted.")
    if succeeded != attempted:
        print("Please scroll up to find which file(s) didn't work out...")
        print("Mayhap Namadu can help figure out what went wrong?")
        print("Anyway...")

    
    return True       

        

#####################################
#      USER INPUT MENUS - Base      #
#####################################

def menu_base():

    done = False
    while not done:
        menu_clear()
        print("Welcome to Namadu's BSEQ tool!")
        print("Which game's BSEQs are you dealing with?")
        print("-"*30)
        print("1. Pokémon X/Y")
        print("2. Pokémon Omega Ruby/Alpha Sapphire")
        print("3. Pokémon Sun/Moon")
        print("4. Pokémon Ultra Sun/Ultra Moon")
        print("5. Pokémon Let's Go Pikachu/Eevee")
        print("6. Pokémon Sword/Shield")
        print("0. Exit")
        print("-"*30)

        getnum = input("> ").strip()

        if getnum == "1":
            done = menu_choose_function_3DS("XY")
        elif getnum == "2":
            done = menu_choose_function_3DS("ORAS")
        elif getnum == "3":
            done = menu_choose_function_3DS("SM")
        elif getnum == "4":
            done = menu_choose_function_3DS("USUM")
        elif getnum == "5":
            done = menu_choose_function_switch("LGPE")
        elif getnum == "6":
            done = menu_choose_function_switch("SwSh")
        elif getnum == "0":
            return False

    input("\n\nPress Enter to close this window.")


def menu_clear():
    print("\n"*10)
    if os.name == 'nt':
        os.system('cls')
    else:
        os.system('clear')

        

#####################################
#     USER INPUT MENUS - Switch     #
##################################### 

def menu_choose_function_switch(game: str):

    done = False
    while not done:
        menu_clear()
        print(f"Okay! We're working with {game}.")
        print("What would you like to do?")
        
        print("-"*30)
        print("1. Convert BSEQ files to JSONs (for easier editing)")
        print("2. Convert JSON files back to BSEQs")
        print("\n3. Convert BSEQ files to a BDSP-specific JSON format")
        print("0. Back")
        print("-"*30)

        getnum = input("> ").strip()
        
        if getnum == "1":
            done = menu_to_json(game)
        elif getnum == "2":
            done = menu_to_bseq(game)
        elif getnum == "3":
            done = menu_to_bdsp(game)    
        elif getnum == "0":
            return False

    return True

        
def menu_to_json(game):
    opts_text = ("NONE - Do not output any group options",
                 "SOME - Only output options with nonzero values (Recommended)",
                 "ALL - Output all group options, even those with zero values")
    
    done = False
    opts_setting = 1
    indents = 2
    while not done:
        menu_clear()
        print(f"Okay, so we're converting {game} BSEQs to JSON format.\n")
        
        print("Make sure that the BSEQs are in the \"inputBSEQs\" folder.")
        print("Any .json files in \"outputJSONs\" may be overwritten.")
        
        print("\nReady to start? Type 1 if so!")
        print("-"*30)
        print("1. DO IT!")
        print("2. [Setting] Group Options: " + opts_text[opts_setting])
        print("3. [Setting] Indents: " + str(indents) + " spaces")      
        print("0. Back")
        print("-"*30)
    
        getnum = input("> ").strip()

        if getnum == "1":
            done = convert_all_bseqs_to_json(game, opts_setting, indents)
        elif getnum == "2":
            opts_setting = (opts_setting+1)%3
        elif getnum == "3":
            indents = (indents+2)%10          
        elif getnum == "0":
            return False

    return True


def menu_to_bseq(game):

    done = False
    while not done:
        menu_clear()
        print("All right! We're gonna convert some JSONs")
        print(f"back into BSEQs for {game}.\n")
        
        print("Make sure that the JSONs are in the \"inputJSONs\" folder.")
        print("Any .BSEQ files in \"outputBSEQs\" may be overwritten.")

        print("\nReady to start? Type 1 if so!")
        print("-"*30)
        print("1. DO IT!")
        print("0. Back")
        print("-"*30)
    
        getnum = input("> ").strip()

        if getnum == "1":
            done = convert_all_jsons_to_bseq(game)           
        elif getnum == "0":
            return False

    return True


def menu_to_bdsp(game):

    format_text = ("ALDO REPACKER",
                   "UABEA")
    opts_text = ("NONE - Do not output any group options",
                 "SOME - Only output options with nonzero values (Recommended)",
                 "ALL - Output all group options, even those with zero values")
    warns_text = ("NONE - Do not output any warnings",
                  "ON W/O COMMENT - Output \"WARNING\" for potentially\n   problematic commands",
                  "ON - Output \"WARNING\" for potentially problematic\n   commands, accompanied by suggested fixes")    
    
    done = False
    format_setting = 0
    opts_setting = 1
    indents = 2
    warns_setting = 2

    while not done:
        menu_clear()
        print(f"Okay, so we're converting {game} BSEQs to JSON files for BDSP.\n")
        
        print("Make sure that the BSEQs are in the \"inputBSEQs\" folder.")
        print("Any .json files in \"outputJSONs\" may be overwritten.")
        print("Note that they can NOT be converted back to BSEQs.")
        
        if opts_setting > 0:
            print(f"\nWARNING: The GroupOptions in {game} might not actually correspond")
            print("  to the same-numbered options in BDSP. This shouldn't matter")
            print("  too much for most single-target move animations, but")
            print("  consider turning Group Options off entirely for now.")
            print("  We can come back to it when we understand it better.\n")
            
            
        print("\nReady to start? Type 1 if so!")
        print("-"*30)
        print("1. DO IT!")
        print("2. [Setting] JSON format: " + format_text[format_setting])
        print("3. [Setting] Group Options: " + opts_text[opts_setting])
        print("4. [Setting] Indents: " + str(indents) + " spaces")
        print("5. [Setting] Warnings: " + warns_text[warns_setting])
        print("0. Back")
        print("-"*30)
    
        getnum = input("> ").strip()

        if getnum == "1":
            done = convert_all_bseqs_to_json(game, opts_setting, indents,
                                             warns_setting, True,
                                             (format_setting == 1))
        elif getnum == "2":
            format_setting = (format_setting+1)%2
        elif getnum == "3":
            opts_setting = (opts_setting+1)%3
        elif getnum == "4":
            indents = (indents+2)%10
        elif getnum == "5":
            warns_setting = (warns_setting+1)%3            
        elif getnum == "0":
            return False

    return True



#####################################
#      USER INPUT MENUS - 3DS       #
##################################### 

def menu_choose_function_3DS(game: str):

    done = False
    while not done:
        menu_clear()
        print(f"Okay! We're working with {game}.")
        print("What would you like to do?\n")
        print("-"*30)
        print("1. Extract BSEQ files from GARC & convert them to JSONs "+
              "(for easier editing)")
        print("2. Import JSONs back into GARC, converting them back to BSEQs")
        print("\n3. Extract raw BSEQ files from GARC")
        print("4. Import raw BSEQ files into GARC")
        print("\n5. Extract BSEQ files from GARC & convert them to a "+
              "BDSP-specific JSON format")
        print("0. Back")
        print("-"*30)

        getnum = input("> ").strip()
        
        if getnum == "1":
            done = menu_extract_garc(game,True)
        elif getnum == "2":
            done = menu_import_garc(game,True)
        elif getnum == "3":
            done = menu_extract_garc(game,False)
        elif getnum == "4":
            done = menu_import_garc(game,False)
        elif getnum == "5":
            done = menu_extract_garc_to_bdsp(game)    
        elif getnum == "0":
            return False

    return True


def menu_extract_garc(game: str, convert_to_json: bool):

    
    opts_text = ("NONE - Do not output any group options",
                 "SOME - Only output options with nonzero values (Recommended)",
                 "ALL - Output all group options, even those with zero values")

    game_garcs = {"XY": "romfs/a/0/3/6", "ORAS": "romfs/a/0/3/4",
                  "SM": "romfs/a/0/8/8", "USUM": "romfs/a/0/8/8"}

    if convert_to_json:
        output_folder = "outputJSONs"
    else:
        output_folder = "outputBSEQs"
    
    done = False
    opts_setting = 1
    indents = 2
    while not done:
        menu_clear()
        print("All right, we're gonna extract all the BSEQs from a GARC file.")
        print(f"For {game}, the file you'll need is {game_garcs[game]}.\n")
        
        print("Put that file in the \"targetGARC\" folder.")
        if convert_to_json:
            print("Its contents will be exported as .json files to")
            print("the \"outputJSONs\" folder. Files there may be overwritten.")
        else:
            print("Its contents will be exported as .bseq files to")
            print("the \"outputBSEQs\" folder. Files there may be overwritten.")
            
        print("\nReady to start? Type 1 if so!")
        print("-"*30)
        print("1. DO IT!")
        # Group Options and Indents are only relevant
        # for JSON outputs.
        if convert_to_json:
            print("2. [Setting] Group Options: " + opts_text[opts_setting])
            print("3. [Setting] Indents: " + str(indents) + " spaces")    
        print("0. Back")
        print("-"*30)
    
        getnum = input("> ").strip()

        if getnum == "1":
            done = extract_garc_files(game, convert_to_json, opts_setting,
                                      indents)
        elif getnum == "2":
            opts_setting = (opts_setting+1)%3
        elif getnum == "3":
            indents = (indents+2)%10                 
        elif getnum == "0":
            return False

    return True


def menu_import_garc(game: str, convert_from_json: bool):

    game_garcs = {"XY": "romfs/a/0/3/6", "ORAS": "romfs/a/0/3/4",
                  "SM": "romfs/a/0/8/8", "USUM": "romfs/a/0/8/8"}

    if convert_from_json:
        input_folder = "inputJSONs"
    else:
        input_folder = "inputBSEQs"
    
    done = False
    while not done:
        menu_clear()
        
        if convert_from_json:
            print("Okay! Any JSON files in the \"inputJSONs\" folder will be")
            print("converted back to BSEQs and imported into a GARC file.")
        else:
            print("Okay! Any BSEQ files in the \"inputBSEQs\" folder")
            print(f"will be imported into a GARC file.")
            
        print(f"For {game}, the file you'll need is {game_garcs[game]}.\n")        
        print("Put that file in the \"targetGARC\" folder.")
        print("Consider making a backup of the GARC first,")
        print("since imported files will overwrite files already in the GARC.")

        print("\nReady to start? Type 1 if so!")
        print("-"*30)
        print("1. DO IT!") 
        print("0. Back")
        print("-"*30)
    
        getnum = input("> ").strip()

        if getnum == "1":
            done = import_garc_files(game, convert_from_json)             
        elif getnum == "0":
            return False

    return True


def menu_extract_garc_to_bdsp(game: str):

    game_garcs = {"XY": "romfs/a/0/3/6", "ORAS": "romfs/a/0/3/4",
                  "SM": "romfs/a/0/8/8", "USUM": "romfs/a/0/8/8"}    

    format_text = ("ALDO REPACKER",
                   "UABEA")
    
    opts_text = ("NONE - Do not output any group options",
                 "SOME - Only output options with nonzero values (Recommended)",
                 "ALL - Output all group options, even those with zero values")
    
    warns_text = ("NONE - Do not output any warnings",
                  "ON W/O COMMENT - Output \"WARNING\" for potentially\n   problematic commands",
                  "ON - Output \"WARNING\" for potentially problematic\n   commands, accompanied by suggested fixes")    
    
    done = False
    format_setting = 0
    opts_setting = 1
    indents = 2
    warns_setting = 2

    while not done:
        menu_clear()
        print("All right, we're gonna extract all the BSEQs from a GARC file")
        print("and convert them to JSONs for use with BDSP.\n")
        
        print(f"For {game}, the file you'll need is {game_garcs[game]}.")
        print("Put that file in the \"targetGARC\" folder.")
        print("Its contents will be exported as .json files to")
        print("the \"outputJSONs\" folder. Files there may be overwritten.")
        print("Note that they can NOT be converted back to BSEQs.")

        
        if opts_setting > 0:
            print(f"\nWARNING: The GroupOptions in {game} might not actually correspond")
            print("  to the same-numbered options in BDSP. This shouldn't matter")
            print("  too much for most single-target move animations, but")
            print("  consider turning Group Options off entirely for now.")
            print("  We can come back to it when we understand it better.\n")
        
        print("\nReady to start? Type 1 if so!")
        print("-"*30)
        print("1. DO IT!")
        print("2. [Setting] JSON format: " + format_text[format_setting])
        print("3. [Setting] Group Options: " + opts_text[opts_setting])
        print("4. [Setting] Indents: " + str(indents) + " spaces")
        print("5. [Setting] Warnings: " + warns_text[warns_setting])  
        print("0. Back")
        print("-"*30)
    
        getnum = input("> ").strip()

        if getnum == "1":
            done = extract_garc_files(game, True, opts_setting,
                                      indents, warns_setting, True,
                                      (format_setting == 1))
        elif getnum == "2":
            format_setting = (format_setting+1)%2
        elif getnum == "3":
            opts_setting = (opts_setting+1)%3
        elif getnum == "4":
            indents = (indents+2)%10
        elif getnum == "5":
            warns_setting = (warns_setting+1)%3                  
        elif getnum == "0":
            return False

    return True



try:
    menu_base()

except (ValueError, FileNotFoundError, PermissionError) as e:
    print(("*"*30+"\n")*3)
    print("Uh oh, an error occurred when reading one of the necessary files:\n",
          "\n"+e.args[0], "\n\n\nAsk Namadu if you don't understand it!")
    print(("*"*30+"\n")*3)
    input("\n\nPress Enter to close the program.")
    
except Exception:
    print(("*"*30+"\n")*6)    
    print("Uh oh an unexpected error happened and the program has to stop!",
          "Show this to Namadu if you don't understand it:\n",
          traceback.format_exc())
    print(("*"*30+"\n")*6)      
    input("\n\nPress Enter to close the program.")
