# Import system modules and arcpy
#
import arcpy, sys, urllib, urllib2
import locale

def xapidownload():
	try:
		# 1. get the input XAPI url : in_xapi_url
		# 1. Récupération de l'URL du serveur XAPI : in_xapi_url
		in_xapi_url = str(arcpy.GetParameterAsText(0))
		AddMsgAndPrint('1. Distant serveur URL : ' + in_xapi_url)

		# 2. get the request extent : request_extent
		# 2. Récupération de l'option de l'emprise : request_extent
		request_extent = str(arcpy.GetParameterAsText(1))
		AddMsgAndPrint('2. Extent option : ' + request_extent)

		# 3. if no extent was set and the default is passed in  -- meaning "#" then set an empty string
		# 3. Récupération de l'emprise de la requête : request_extent
		# Si l'emprise n'a pas été spécifiée ou que l'option "emprise par défaut" est choisie (c'est à dire "#"),
		# alors la valeur est une chaine de caractères vide
		if (request_extent == '#'):
			request_extent = ''
			AddMsgAndPrint('3.1. Empty extent : ' + request_extent)
		elif (request_extent == 'DEFAULT'):
			request_extent = ''
			AddMsgAndPrint('3.2. Default extent : ' + request_extent)
		else:
			coordinates = request_extent.split(' ')
			request_extent = str(locale.atof(coordinates[0])) + ',' +  str(locale.atof(coordinates[1])) + ',' + str(locale.atof(coordinates[2])) + ',' + str(locale.atof(coordinates[3]))
			AddMsgAndPrint('3.3.1 Extent: ' + request_extent)
			request_extent = CheckForBrackets('bbox=' + request_extent)
			AddMsgAndPrint('3.3.2 Extent request : ' + request_extent)
			request_extent = urllib.quote_plus(request_extent)
			AddMsgAndPrint('3.3.3 URL Lib : ' + request_extent)

		# 4. get the type setting : request_type
		# this can one of the following parameters
		# 4. Récupération des paramètres types : request_type
		# 	Les valeurs des paramètres peuvent être :
		# * - meaning all (tous)
		# 	node (noeuds)
		# 	way (lignes)
		# 	relation (relations)
		request_type = str(arcpy.GetParameterAsText(2))
		AddMsgAndPrint('4. Request type : ' + request_type)
		#request_type = urllib.quote(request_type)

		# 5. get the predicate string : request_predicate
		# 5. Récupération de la chaine de prédicat : request_predicate
		request_predicate = str(arcpy.GetParameterAsText(3))
		if (len(request_predicate) != 0):
			request_predicate = CheckForBrackets(request_predicate)
			request_predicate = urllib.quote(request_predicate)
			AddMsgAndPrint('5. Predicate : ' + request_predicate)

		# 6. get the location for the downloaded file : xapi_osm_file
		# 6. Récupération de l'adresse du fichier téléchargé : xapi_osm_file
		xapi_osm_file = arcpy.GetParameterAsText(4)

		# 7. assemble the request url
		# sample: http://jxapi.openstreetmap.org/xapi/api/0.6/*%5Bname=Sylt%5D
		# 7. Assemble la requête url
		request_url = in_xapi_url + request_type + request_predicate + request_extent
		AddMsgAndPrint('7. Request URL : ' + request_url)

		# 8. issue the request against the XAPI endpoint
		# 8. Emission de la requête avec les critères XAPI
		try:
			xapi_response = urllib2.urlopen(request_url)
		except urllib2.URLError, e:
			if hasattr(e, 'reason'):
				AddMsgAndPrint('8.1. Unable to reach the server.', 2)
				AddMsgAndPrint(e.reason, 2)
			elif hasattr(e, 'code'):
				AddMsgAndPrint('8.2. The server was unable to fulfill the request.', 2)
				AddMsgAndPrint(e.code, 2)

		else:
			# write the content into a file on disk
			# Ecrit le contenu de la requête dans un fichier sur le disque
			with open(xapi_osm_file,'w') as xapifile:
				xapifile.write(xapi_response.read())

	except Exception as err:
		AddMsgAndPrint('99. Error ' + err.message, 2)

	# 9. Check for brackets
	# 9. Vérification de la présence des crochets
def CheckForBrackets(inputString):
	paddedString = inputString

	if (paddedString[0] != '['):
		paddedString = '[' + paddedString

	if (paddedString[-1] != ']'):
		paddedString = paddedString + ']'

	return paddedString


def AddMsgAndPrint(msg, severity=0):
    # 10. Adds a Message (in case this is run as a tool)
    # and also prints the message to the screen (standard output)
    # 10. Ajoute un message dans le cas où le script est lancé comme outil
    # et affiche aussi le message à l'écran (sortie standard)
    print msg

    # 11. Split the message on \n first, so that if it's multiple lines,
    #  a GPMessage will be added for each line
    # 11. Coupe le message sur le premier \n pour que, en cas de message multi-lignes, 
    # un message GP soit ajouté à chaque ligne
    try:
        for string in msg.split('\n'):
            # Add appropriate geoprocessing message
            # Ajoute le message de géoprocess approprié
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
