# TODO: add error checking & messages for command references.
# Since I tell users not to edit this, it shouldn't be an issue.

class ParamRef:
    """
    The definition for a single parameter in a command.
    At minimum, includes a list of possible names.
    May also include expected param type, default value,
    and byte length (if different than 4).
    """

    def __init__(self, names: list, value_type="Unknown", default=0,
                 length=4):
        self.names = names
        self.value_type = value_type
        self.default = default
        self.length = length


class CmdDef:
    """
    The definition for a single command type.
    At minimum, includes the ID (string), list of possible names,
    a list of parameters, and the total byte length of those parameters.
    """

    def __init__(self, dictionary: dict):
        """
        Initializes a CmdDef instance.
        :param dictionary: a single entry from the CommandReference json.
        """

        # Warnings are only in the BDSP ref.
        if "WARNING" in dictionary:
            self.warning = dictionary["WARNING"]
        else:
            self.warning = False
        
        self.id = dictionary["commandID"]
        self.names = dictionary["commandNames"]
        self.param_bytes = dictionary["totalParamBytes"]
        self.params = []
        self.name_to_idx = {}

        calculated_param_bytes = 0

        for i, param in enumerate(dictionary["paramList"]):
            names = param["paramNames"]

            # To speed up future calculations a tad.
            for name in names:
                self.name_to_idx[name] = i

            try:
                value_type = param["valueType"]
            except KeyError:
                value_type = "Unknown"

            try:
                default = param["defaultValue"]
            except KeyError:
                if value_type == "Hex":
                    default = ""
                elif value_type == "String":
                    default = ""
                elif value_type == "Float":
                    default = 0.0
                elif value_type == "Bool":
                    default = True
                elif value_type == "ListBool":
                    default = [True, True, True]
                elif value_type == "ListInt":
                    default = [0, 0, 0]
                elif value_type == "ListFloat":
                    default = [0.0, 0.0, 0.0]
                else:
                    default = 0

            try:
                length = param["paramBytes"]
            except KeyError:
                if value_type in ("ListFloat", "ListInt", "ListBool"):
                    length = 12
                else:
                    length = 4

            param_obj = ParamRef(names, value_type, default, length)

            self.params.append(param_obj)

            calculated_param_bytes += length

        # To catch an oopsie that might occur when editing the reference file.
        if calculated_param_bytes != self.param_bytes:
            error = "totalParamBytes seems to be incorrect for the command\n"
            error += f"with ID {self.id}; params add up to {calculated_param_bytes}"
            error += f"\nbut totalParamBytes says {self.param_bytes}."
            raise ValueError(error)


class CmdReference:
    """
    Contains info on all command types and their expected parameters.
    """

    def __init__(self, ref_dict: dict):
        # Handle just 1 case for now. Could add more later.
        # Case: a dictionary imported from a JSON is passed.

        # After reading in the json, get this data:
        # - The game. (SwSh)
        # - An array of cmdDef objects.

        # Primary dictionary's keys are command IDs, as hex strings.
        self.commands = {}

        # For quicker lookups later.
        self.cmd_name_dict = {}

        for command_dict in ref_dict["commandInfo"]:
            command_def = CmdDef(command_dict)
            curr_id = command_def.id

            self.commands[curr_id] = command_def

            for name in command_def.names:
                self.cmd_name_dict[name] = curr_id

        self.game = ref_dict["game"]

        if self.game == "SwSh":
            self.group_option_ids = ref_dict["groupOptionIDs"]


    def name_to_id(self, name: str):
        """
        :param name: The command's name.
        :return: That command's ID.
        """
        return self.cmd_name_dict[name]

    def id_to_name(self, cmd_id: str):
        """
        :param cmd_id: The command's ID, as a hex string.
        :return: That command's default name.
        """
        return self.commands[cmd_id].names[0]

    def params_length(self, cmd_id: str):
        """
        :param cmd_id: The command's ID, as a hex string.
        :return: That command's total parameter byte length.
        """
        return self.commands[cmd_id].param_bytes

    def lookup_params(self, cmd_id: str):
        """
        :param cmd_id: The command's ID, as a hex string.
        :return: That command's list of parameters.
        """
        return self.commands[cmd_id].params
