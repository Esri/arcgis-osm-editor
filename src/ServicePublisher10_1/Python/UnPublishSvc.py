import sys
import urllib2 
import os
import httplib
import string
import mimetypes 
import urllib
import json

from optparse import OptionParser

# A function that checks that the input JSON object 
#  is not an error object.    
def assertJsonSuccess(data):
    obj = json.loads(data)
    if 'status' in obj and obj['status'] == "error":
        print "Error: JSON object returns an error. " + str(obj)
        return False
    else:
        return True

try:

    # Setup parser to parse arguments
    parser = OptionParser()
    parser.add_option("-u", "--agsurl", action="store", type="string", dest="agsInstanceURL")
    parser.add_option("-n", "--username", action="store", type="string", dest="username")
    parser.add_option("-p", "--password", action="store", type="string", dest="password")
    parser.add_option("-s", "--svcName", action="store", type="string", dest="svcName")
    parser.add_option("-f", "--svcFolder", action="store", type="string", dest="svcFolder")
    
    (options, args) = parser.parse_args()

    print options.agsInstanceURL
    print options.username
    #print options.password
    print options.svcName
    print options.svcFolder

    agsInstanceURL = options.agsInstanceURL
    svcName = options.svcName
    svcFolder = options.svcFolder    
    if svcFolder != None:
        svcFolder = svcFolder + '/'
    else:
        svcFolder = ''
    
    # Set port
    indexPort = str.find(agsInstanceURL, ':', 6)
    indexFwdSlsh = str.find(agsInstanceURL, '/', indexPort)
    serverName = agsInstanceURL[7:indexPort]
    serverPort = agsInstanceURL[indexPort + 1:indexFwdSlsh]

    # Get Token
    theurl = agsInstanceURL + '/tokens/generateToken'
    username = options.username
    password = options.password
    params = urllib.urlencode({'request': 'gettoken', 'username': username, 'password': password})

    try:
        pagehandle = urllib2.urlopen(theurl, params)
    except IOError, e:
        if hasattr(e, 'code'):
            if e.code != 401:                
                sys.exit(e.code)
            else:
                sys.exit(e)

    token = pagehandle.read()
   

    # Delete the service    
    theurl = agsInstanceURL + '/admin/services/' + svcFolder + svcName + '.MapServer/delete?f=json&token='+ token
   
    # This request only needs the token and the response formatting parameter 
    params = urllib.urlencode({'token': token, 'f': 'json'})
    
    headers = {"Content-type": "application/x-www-form-urlencoded", "Accept": "text/plain"}
    
    # Connect to URL and post parameters    
    httpConn = httplib.HTTPConnection(serverName, serverPort)
    httpConn.request("POST", theurl, params, headers)
    
    # Read response
    response = httpConn.getresponse()
    if (response.status != 200):
        httpConn.close()
        print "Could not delete service."
        sys.exit("Could not delete service.")
    else:
        data = response.read()
        
        # Check that data returned is not an error object
        if not assertJsonSuccess(data):
            httpConn.close()            
            print "Error when deleting service " + svcName + ". " + str(data)
            sys.exit("Error when deleting service " + svcName + ". " + str(data))
        else:
            print "Deletion of service " + svcName + " successful."

        # Deserialize response into Python object
        dataObj = json.loads(data)
        httpConn.close()    

except:
    print "Unexpected error in UnPublishSvc:", sys.exc_info()[0]
    raise



