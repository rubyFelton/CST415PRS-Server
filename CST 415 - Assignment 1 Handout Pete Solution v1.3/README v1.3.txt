README - Assignment 1 Pete Solution
v1.3
9/2/2019

Keep these files in the same directory.
The client and server both accept command line arguments.
Try PRSSerer -help and PRSTestClient -help to see the usage statement.
The test client tells the server to exit at the end of the tests.
The test client expects the server to be run with specific arguments...
	PRSServer.exe -p 30000 -s 40000 -e 40100 -t 10

To see these in action:
1. Start two command prompts from this directory
2. In the first command prompt, run PRSServer.exe -p 30000 -s 40000 -e 40100 -t 10
3. In the second command prompt, run PRSTestClient.exe
4. Wait until both have printed the text "Closed!"
5. Hit the Enter key in both command prompts

