import arcpy, os, sys, urllib, urllib2, re, traceback
from xml.etree.ElementTree import ElementTree, Element, SubElement

def open_nds():
    try:
        in_nds = arcpy.GetParameterAsText(0)
        out_xml = arcpy.GetParameterAsText(1)
        AddMsgAndPrint('Network dataset: ' + in_nds)
        AddMsgAndPrint('Output XML file: ' + out_xml)

        arcpy.env.workspace = os.path.dirname(os.path.dirname(in_nds))

        # Create Describe object for the network dataset
        desc = arcpy.Describe(in_nds)

        # root = networkConfiguration
        root = Element('networkConfiguration')

        # Edges
        for edgeSource in desc.edgeSources:
            edge = gen_edge_element(edgeSource)
            if (edge is not None):
                root.append(edge)

        # Junctions
        for junctionSource in desc.junctionSources:
            junction = gen_junction_element(junctionSource)
            if (junction is not None):
                root.append(junction)

        # Turns
        if desc.supportsTurns:
            turn = gen_turn_element(desc.turnSources)
            if (turn is not None):
                root.append(turn)

        # Connectivity
        connectivity = gen_connectivity_element(desc.edgeSources, desc.junctionSources)
        if (connectivity is not None):
            root.append(connectivity)

        # Directions
        if (desc.supportsDirections):
            directions = gen_directions_element(desc.directions)
            if (directions is not None):
                root.append(directions)

        # Network Attributes
        network = gen_network_element(desc.attributes)
        if (network is not None):
            root.append(network)

        #Write XML to a file
        file = open(out_xml, 'w')
        try:
            ElementTree(root).write(file)
        except:
            # Get the traceback object
            tb = sys.exc_info()[2]
            tbinfo = traceback.format_tb(tb)[0]
            # Concatenate error information into message string
            pymsg = 'PYTHON ERRORS:\nTraceback info:\n{0}\nError Info:\n{1}'\
                .format(tbinfo, str(sys.exc_info()[1]))
            msgs = 'ArcPy ERRORS:\n {0}\n'.format(arcpy.GetMessages(2))
            # Return python error messages for script tool or Python Window
            arcpy.AddError(pymsg)
            arcpy.AddError(msgs)
        finally:
            file.close()

    except:
        # Get the traceback object
        tb = sys.exc_info()[2]
        tbinfo = traceback.format_tb(tb)[0]
        # Concatenate error information into message string
        pymsg = 'PYTHON ERRORS:\nTraceback info:\n{0}\nError Info:\n{1}'\
            .format(tbinfo, str(sys.exc_info()[1]))
        msgs = 'ArcPy ERRORS:\n {0}\n'.format(arcpy.GetMessages(2))
        # Return python error messages for script tool or Python Window
        arcpy.AddError(pymsg)
        arcpy.AddError(msgs)


# Build <edge> element for a NetworkDataset.edgeSource
def gen_edge_element(source):
    edge = Element("edge")

    name = SubElement(edge, "name")
    name.text = UnqualifiedTableName(source.name)

    connect = SubElement(edge, "connect_policy")
    connect.text = source.connectivityPolicies.classConnectivity

    # add an empty query element
    query = SubElement(edge, "query")

    if (source.sourceDirections):
        street_name_fields = SubElement(edge, "street_name_fields")
        for fields in source.sourceDirections.streetNameFields:
            if fields.prefixDirectionFieldName:
                fldTag = SubElement(street_name_fields, "direction_prefix")
                fldTag.text = fields.prefixDirectionFieldName

            if fields.prefixTypeFieldName:
                fldTag = SubElement(street_name_fields, "type_prefix")
                fldTag.text = fields.prefixTypeFieldName

            if fields.streetNameFieldName:
                fldTag = SubElement(street_name_fields, "street_name")
                fldTag.text = fields.streetNameFieldName

            if fields.suffixDirectionFieldName:
                fldTag = SubElement(street_name_fields, "direction_suffix")
                fldTag.text = fields.suffixDirectionFieldName

            if fields.suffixTypeFieldName:
                fldTag = SubElement(street_name_fields, "type_suffix")
                fldTag.text = fields.suffixTypeFieldName

    return edge


# Build <junction> element for a NetworkDataset.junctionSource
def gen_junction_element(source):
    if (source.sourceType != "JunctionFeature"):
        return None

    junction = Element("junction")

    name = SubElement(junction, "name")
    name.text = UnqualifiedTableName(source.name)

    connect = SubElement(junction, "connect_policy")
    connect.text = source.connectivityPolicies.classConnectivity

    # add an empty query element
    query = SubElement(junction, "query")

    return junction


# Build <turn> element for a NetworkDataset.turnSources list
def gen_turn_element(turnSources):
    turn = None

    if len(turnSources) > 0:
        turn = Element("turn")

        name = SubElement(turn, "name")
        name.text = UnqualifiedTableName(turnSources[0].name)

    return turn


# Build <connectivity> element for a NetworkDataset.NetworkSources list
def gen_connectivity_element(edgeSources, junctionSources):
    connectivity = Element("connectivity")

    grouplist = {}
    for edge in edgeSources:
        grp = edge.connectivityPolicies.defaultGroup
        if (grp in grouplist):
            srcTag = SubElement(grouplist[grp], "source")
            srcTag.text = UnqualifiedTableName(edge.name)
        else:
            grpTag = Element("group")
            srcTag = SubElement(grpTag, "source")
            srcTag.text = UnqualifiedTableName(edge.name)
            grouplist[grp] = grpTag

    for junction in junctionSources:
        if (junction.sourceType != "JunctionFeature"):
            continue

        for i in range(0, junction.connectivityPolicies.defaultGroupsCount):
            grp = getattr(junction.connectivityPolicies, "defaultGroupName" + str(i))
            if (grp in grouplist):
                srcTag = SubElement(grouplist[grp], "source")
                srcTag.text = UnqualifiedTableName(junction.name)
            else:
                grpTag = Element("group")
                srcTag = SubElement(grpTag, "source")
                srcTag.text = UnqualifiedTableName(junction.name)
                grouplist[grp] = grpTag

    for grp in grouplist:
        connectivity.append(grouplist[grp])

    return connectivity


# Build <directions> element for a NetworkDataset.directions list
def gen_directions_element(sourceDirections):
    directions = None

    if sourceDirections:
        directions = Element("directions")

        if (sourceDirections.lengthAttributeName):
            fldTag = SubElement(directions, "length_attr")
            fldTag.text = sourceDirections.lengthAttributeName

        if (sourceDirections.defaultOutputLengthUnits):
            fldTag = SubElement(directions, "length_units")
            fldTag.text = sourceDirections.defaultOutputLengthUnits

        if (sourceDirections.timeAttributeName):
            fldTag = SubElement(directions, "time_attr")
            fldTag.text = sourceDirections.timeAttributeName

        if (sourceDirections.roadClassAttributeName):
            fldTag = SubElement(directions, "road_class_attr")
            fldTag.text = sourceDirections.roadClassAttributeName

    return directions


# Build <network> element for a NetworkDataset.attributes list
def gen_network_element(attributes):
    try:
        if len(attributes) == 0:
            return None

        network = Element("network")

        for attr in attributes:
            netattr = SubElement(network, "network_attribute")

            name = SubElement(netattr, "name")
            name.text = attr.name

            default = SubElement(netattr, "default_value")
            if (attr.defaultEdgeEvaluatorType == "Constant"):
                default.text = str(attr.defaultEdgeData).lower()
            else:
                default.text = 0

            if (attr.usageType == "Cost"):
                evaluator = SubElement(netattr, "cost", { "useAsDefault": str(attr.useByDefault).lower() })

                units = SubElement(evaluator, "units")
                units.text = attr.units

                dt = SubElement(evaluator, "datatype")
                dt.text = attr.dataType.lower()

                for i in range(0, attr.parameterCount):
                    gen__parameter_element(attr, evaluator, i)

                for i in range(0, attr.evaluatorCount):
                    gen_evaluator_element(evaluator, attr, i)

            elif (attr.usageType == "Restriction"):
                evaluator = SubElement(netattr, "restriction", { "useAsDefault": str(attr.useByDefault).lower() })

                for i in range(0, attr.parameterCount):
                    gen__parameter_element(attr, evaluator, i)

                for i in range(0, attr.evaluatorCount):
                    gen_evaluator_element(evaluator, attr, i)

            elif (attr.usageType == "Descriptor"):
                evaluator = SubElement(netattr, "descriptor")

                dt = SubElement(evaluator, "datatype")
                dt.text = attr.dataType.lower()

                for i in range(0, attr.parameterCount):
                    gen__parameter_element(attr, evaluator, i)

                for i in range(0, attr.evaluatorCount):
                    gen_evaluator_element(evaluator, attr, i)

            elif (attr.usageType == "Hierarchy"):
                evaluator = SubElement(netattr, "hierachy", { "useAsDefault": str(attr.useByDefault).lower() })

                for i in range(0, attr.parameterCount):
                    gen__parameter_element(attr, evaluator, i)

                for i in range(0, attr.evaluatorCount):
                    gen_evaluator_element(evaluator, attr, i)
    except:
        # Get the traceback object
        tb = sys.exc_info()[2]
        tbinfo = traceback.format_tb(tb)[0]
        # Concatenate error information into message string
        pymsg = 'PYTHON ERRORS:\nTraceback info:\n{0}\nError Info:\n{1}'\
            .format(tbinfo, str(sys.exc_info()[1]))
        msgs = 'ArcPy ERRORS:\n {0}\n'.format(arcpy.GetMessages(2))
        # Return python error messages for script tool or Python Window
        arcpy.AddError(pymsg)
        arcpy.AddError(msgs)

    return network


# Builds <parameter> element
def gen__parameter_element(attr, evaluator, i):
    param = SubElement(evaluator, "parameter")

    pname = SubElement(param, "name")
    pname.text = getattr(attr, "parameterName" + str(i))

    pdt = SubElement(param, "datatype")
    pdt.text = getattr(attr, "parameterType" + str(i)).lower()

    pdefault = SubElement(param, "default_value")
    pdefault.text = str(getattr(attr, "parameterDefaultValue" + str(i))).lower()


# Builds <evaluator_attributes> element
def gen_evaluator_element(evaluator, attr, i):
    try:
        eval_attr = SubElement(evaluator, "evaluator_attributes")

        source = SubElement(eval_attr, "source")
        source.text = UnqualifiedTableName(getattr(attr, "sourceName" + str(i)))

        dir = SubElement(eval_attr, "direction")
        dir.text = getattr(attr, "edgeDirection" + str(i))
        if (dir.text == "AgainstDigitizedDirection"):
            dir.text = "To-From"
        else:
            dir.text = "From-To"

        et = getattr(attr, "evaluatorType" + str(i))
        if (et == "Constant"):
            constant = SubElement(eval_attr, "Constant")
            value = SubElement(constant, "value")
            if hasattr(attr, "data" + str(i)):
                value.text = str(getattr(attr, "data" + str(i))).lower()

        elif (et == "Field"):
            field = SubElement(eval_attr, "Field", { "script_type": "VBScript" })

            expr = getattr(attr, "data" + str(i))
            match = re.search("(\nvalue = \w+)", expr)
            if (match is None):
                expression = SubElement(field, "expression")
                expression.text = expr
            else:
                expression = SubElement(field, "expression")
                expression.text = match.group(0).replace("value = ", "").strip()

                prelogic = SubElement(field, "pre_logic")
                prelogic.text = expr[0:match.start(0)].replace("\r", "")

        else:
            script = SubElement(eval_attr, "Script", { "script_type": "VBScript" })

            expr = getattr(attr, "data" + str(i))
            match = re.search("(\nvalue = \w+)", expr)
            if (match is None):
                expression = SubElement(script, "expression")
                expression.text = expr
            else:
                expression = SubElement(script, "expression")
                expression.text = match.group(0).replace("value = ", "").strip()

                prelogic = SubElement(script, "pre_logic")
                prelogic.text = expr[0:match.start(0)].replace("\r", "")

    except:
        # Get the traceback object
        tb = sys.exc_info()[2]
        tbinfo = traceback.format_tb(tb)[0]
        # Concatenate error information into message string
        pymsg = 'PYTHON ERRORS:\nTraceback info:\n{0}\nError Info:\n{1}'\
            .format(tbinfo, str(sys.exc_info()[1]))
        msgs = 'ArcPy ERRORS:\n {0}\n'.format(arcpy.GetMessages(2))
        # Return python error messages for script tool or Python Window
        arcpy.AddError(pymsg)
        arcpy.AddError(msgs)

def UnqualifiedTableName(path):
    return arcpy.ParseTableName(path).split(",")[2].strip()


def AddMsgAndPrint(msg, severity = 0):
    # Adds a Message (in case this is run as a tool)
    # and also prints the message to the screen (standard output)
    print msg

    # Split the message on \n first, so that if it's multiple lines,
    #  a GPMessage will be added for each line
    try:
        for string in msg.split('\n'):
            # Add appropriate geoprocessing message
            if severity == 0:
                arcpy.AddMessage(string)
            elif severity == 1:
                arcpy.AddWarning(string)
            elif severity == 2:
                arcpy.AddError(string)
    except:
        pass


if __name__ == '__main__':
    open_nds()
