Microsoft (R) TINYGET Version 5.2
Copyright (C) 2003 Microsoft Corporation. All rights reserved.


Usage:
TINYGET [-a:auth] [-m:domain] [-u:username] [-p:password] [-v:http version] [-t] [-h] [-d] [-k] [-l:iterations] [-r:port] [-s:SSL protocol] [-w] <server name> <URL>

----------------------------------------
 
SETTING AUTHENTICATION METHOD AND USER 

----------------------------------------
   
[-a:auth] or [-auth:auth]   - authentication method
        - 0=anon, 1=basic, 2=ntlm, 3=kerberos 4=digest 5=negotiate 111=random
          (both numeric codes or text values can be used)
        - default is 

anonymous
        - if ntlm authentication is used, the -k (keep-alive)
            is turned on automatically
        - if basic or ntlm authentication is used, domain,
            user name, and user password 

should be specified
   
[-domain:NAME] [-m:NAME]    - user domain
   
[-pass:NAME] [-p:NAME]      - user password
   
[-user:NAME] or [-u:NAME]   - user name
   
[-u:"DOMAIN\USER" "PASSWORD"]]
                               - user domain, name and password
                               - specify "CurrentUser" (case sensitive!) to use currently logged on user 

credentials

---------------------------
 
SETTING SECURE CONNECTION 

---------------------------
   
[-s:SSL protocol]      - secure channel protocol to use
           - 0 = no secure channel, 1=PCT1 2=SSL2, 3=SSL3, 4=TLS
           - default is no secure channel, PCT1,SSL2,SSL3,TLS1 can be used as parameter 
 

            instead of numeric codes
   
[-cipher:CIPHER]       - cipher for SSL 
           - 0 = default, RC2, RC4, DES, 3DES
           - 0 means that it is up to schannel to pick cipher

-------------------------------------------
 
CLIENT CERTIFICATES FOR SECURE CONNECTION 

-------------------------------------------
   
[-keypair:"fromIE" SubjectNAME]
           - use "fromIE" keyword to use the same certificates that are available from IE
   
[-c:certificate]          - sequence number of certificate from MYSTORE to use (default is 1)

----------------
 
OUTPUT CONTROL 

----------------
   
[-data] or [-d]        - display result data
   
[-headers] or [-h]     - display result headers
   
[-trace] or [-t]       - output detailed trace information
         - this option will output the request sent, the header received, and the
           data received as well as other relevant trace information

-------------------------------------------
 
LOOPING, MULTITHREADING AND SCRIPTING 

-------------------------------------------
   
[-threads:NUMBER] or [-x:NUMBER]   - number of concurrent threads
   
[-loop:NUMBER] or [-l:NUMBER]      - number of times to do the specified request
   
[-script:NAME] or [-z:NAME]        - script name
         - when this option is used, a lot of control is lost, authentication and
           secure channel protocol default to whatever wininet does
   
[-y]                               - read script in random order

----------------------
 
HTTP REQUEST CONTROL 

----------------------
   
[-verb:NAME] or [-j:NAME]          - request verb (GET,PUT,DELETE,HEAD,TRACE.POST)
          (GET by default)
   
[-ver:NUMBER] or [-v:NUMBER]       - http version to use
            - (0 - http 1.0, 1 - http 1.1)
            - default is 1.0
   
[-reqbody:BODY] or [-rb:BODY]      - request body
   [-freqbody:BODY] or [-frb:BODY]    - file with request body
   
[-reqheaders:BODY] or [-rh:BODY]   - additional request headers
   [-freqheaders:BODY] or [-frh:BODY] - file with additional request body

----------------------
 
Expected results 

----------------------
   
[-status:number                 - expect status code
   
[-testContainString:value]      - response body has to contain string
   
[-testNotContainString:value]   - response body must not contain string
   
[-testEqString:value]           - response body has to equal string
   
[-testEqFile:filename]          - response body has to be the same as given file content
   
[-hostheader:NAME] [-n:NAME]    - add host header to request with given name
         - default host name is "server name"
   
[-reqraw:RAWREQUEST             - raw request to be send to server
         - Note: all other request settings are ignored when raw request is used 

---------
 
VARIOUS 

---------
   
[-b]  - catch exceptions and abort (use only for stress)
   
[-o]                          - reuse socket for multiple connections
   
[-SockTimeout: send receive]  - set socket timeout in miliseconds (default is 18000)
   
[-port:NUMBER] or [-r:NUMBER] - port to connect on - default is 80
   
[-replayheader:HEADERNAME]or [-reph:HEADERNAME] - replay received header in the response
   
[-initreplayheader]           - reset replay header
   
[-buffersize]                 - set response buffer size (default is 100 000 bytes
---------
 PROXY 
---------
   
[-proxy: <ProxyName> <ProxyPort>]   - use proxy

----------------------------------------------
 
MANDATORY PARAMETERS 

----------------------------------------------
   
[-srv:<server name>]   - name of server to connect to
   
[-uri:<URI>]           - URI (path) to get