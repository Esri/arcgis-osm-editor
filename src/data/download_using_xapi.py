# Import system modules and arcpy
#
import arcpy, urllib, urllib2
import locale


def xapidownload():
    try:
        # get the input XAPI url
        #
        in_xapi_url = arcpy.GetParameterAsText(0)

        # get the request extent
        #
        request_extent = arcpy.GetParameterAsText(1)
        AddMsgAndPrint(request_extent)

        # if no extent was set and the default is passed in  -- meaning "#" then set an empty string
        if request_extent == '#':
            request_extent = ''
        elif request_extent == 'DEFAULT':
            request_extent = ''
        else:
            coordinates = request_extent.split(' ')
            request_extent = str(locale.atof(coordinates[0])) + ',' + str(locale.atof(coordinates[1])) + ',' + str(
                locale.atof(coordinates[2])) + ',' + str(locale.atof(coordinates[3]))
            request_extent = CheckForBrackets('bbox=' + request_extent)
            request_extent = urllib.quote_plus(request_extent)

        # get the type setting
        # this can one of the following parameters
        # * - meaning all
        # node
        # way
        # relation
        #
        request_type = arcpy.GetParameterAsText(2)
        # request_type = urllib.quote(request_type)

        # get the predicate string
        #
        request_predicate = arcpy.GetParameterAsText(3)
        if len(request_predicate) != 0:
            request_predicate = CheckForBrackets(request_predicate)
            request_predicate = urllib.quote(request_predicate)

        # get the location for the downloaded file
        #
        xapi_osm_file = arcpy.GetParameterAsText(4)

        # assemble the request url
        # sample: http://jxapi.openstreetmap.org/xapi/api/0.6/*%5Bname=Sylt%5D
        #
        request_url = in_xapi_url + request_type + request_predicate + request_extent
        AddMsgAndPrint(request_url)

        # issue the request against the XAPI endpoint
        #
        try:
            xapi_response = urllib2.urlopen(request_url)
        except urllib2.URLError, e:
            if hasattr(e, 'reason'):
                AddMsgAndPrint('Unable to reach the server.', 2)
                AddMsgAndPrint(e.reason, 2)
            elif hasattr(e, 'code'):
                AddMsgAndPrint('The server was unable to fulfill the request.', 2)
                AddMsgAndPrint(e.code, 2)

        else:
            # write the content into a file on disk
            #
            with open(xapi_osm_file, 'w') as xapifile:
                xapifile.write(xapi_response.read())

    except Exception as err:
        AddMsgAndPrint(err.message, 2)


def CheckForBrackets(inputString):
    paddedString = inputString

    if paddedString[0] != '[':
        paddedString = '[' + paddedString

    if paddedString[-1] != ']':
        paddedString += ']'

    return paddedString


def AddMsgAndPrint(msg, severity=0):
    # Adds a Message (in case this is run as a tool)
    # and also prints the message to the screen (standard output)
    #
    print msg

    # Split the message on \n first, so that if it's multiple lines,
    #  a GPMessage will be added for each line
    try:
        for string in msg.split('\n'):
            # Add appropriate geoprocessing message
            #
            if severity == 0:
                arcpy.AddMessage(string)
            elif severity == 1:
                arcpy.AddWarning(string)
            elif severity == 2:
                arcpy.AddError(string)
    except:
        pass


if __name__ == '__main__':
    xapidownload()
