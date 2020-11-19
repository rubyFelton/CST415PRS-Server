// PRSServerProgram.cs
// Oregon Institute of Technology
// Pete Myers
// CST 415 Networks
// Fall 2020
// 
//Ruby Felton
//10/08/2020

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using PRSLib;

namespace PRSServer
{
    class PRSServerProgram
    {
        class PRS
        {
            // represents a PRS Server, keeps all state and processes messages accordingly

            class PortReservation
            {
                private ushort port;
                private bool available;
                private string serviceName;
                private DateTime lastAlive;

                public PortReservation(ushort port)
                {
                    this.port = port;
                    available = true;
                }

                public string ServiceName { get { return serviceName; } }
                public ushort Port { get { return port; } }
                public bool Available { get { return available; } }

                public bool Expired(int timeout)
                { 
                    // return true if timeout seconds have elapsed since lastAlive
                    return DateTime.Now > lastAlive.AddSeconds(timeout);
                }

                public void Reserve(string serviceName)
                {
                    // reserve this port for serviceName
                    available = false;
                    this.serviceName = serviceName;
                    lastAlive = DateTime.Now;
                }

                public void KeepAlive()
                {
                    // save current time in lastAlive
                    lastAlive = DateTime.Now;
                }

                public void Close()
                {
                    // make this reservation available
                    available = true;
                    serviceName = null;
                }
            }

            // server attribues
            private ushort startingClientPort;
            private ushort endingClientPort;
            private int keepAliveTimeout;
            private int numPorts;
            private PortReservation[] ports;
            private bool stopped;

            public PRS(ushort startingClientPort, ushort endingClientPort, int keepAliveTimeout)
            {

                // save parameters
                this.startingClientPort = startingClientPort;
                this.endingClientPort = endingClientPort;
                this.keepAliveTimeout = keepAliveTimeout;
                
                // initialize to not stopped
                stopped = false;

                // initialize port reservations
                numPorts = endingClientPort - startingClientPort + 1; //inclusive
                ports = new PortReservation[numPorts];

                //loop through port res array, fill in port numbers
                for (ushort port = startingClientPort; port <= endingClientPort; port++)
                {
                    //array is zero based, port numbers start at starting port
                    ports[port - startingClientPort] = new PortReservation(port);
                }
            }

            public bool Stopped { get { return stopped; } }

            private void CheckForExpiredPorts()
            {
                // expire any ports that have not been kept alive with in time out
                foreach (PortReservation reservation in ports)
                {
                    //if port is not available and is passed keep alive time out close it
                    if (!reservation.Available && reservation.Expired(keepAliveTimeout))
                    {
                        reservation.Close();
                    }
                }
            }

            private PRSMessage RequestPort(string serviceName)
            {
                PRSMessage response = null;

                if (ports.SingleOrDefault(p => p.ServiceName == serviceName && !p.Available) == null)
                {
                    // client has requested the lowest available port, so find it!
                    PortReservation reservation = null;
                    reservation = ports.FirstOrDefault(p => p.Available);

                    // if found an avialable port, reserve it and send SUCCESS
                    if (reservation != null)
                    {
                        reservation.Reserve(serviceName);
                        response = new PRSMessage(PRSMessage.MESSAGE_TYPE.RESPONSE, serviceName, reservation.Port, PRSMessage.STATUS.SUCCESS);
                    }
                    else
                    {
                        response = new PRSMessage(PRSMessage.MESSAGE_TYPE.RESPONSE, serviceName, 0, PRSMessage.STATUS.ALL_PORTS_BUSY);

                    }
                }
                else
                {
                    response = new PRSMessage(PRSMessage.MESSAGE_TYPE.RESPONSE, serviceName, 0, PRSMessage.STATUS.SERVICE_IN_USE);
                }

                return response;
            }

            public PRSMessage HandleMessage(PRSMessage msg)
            {
                // handle one message and return a response

                PRSMessage response = null;

                //check for expired ports
                CheckForExpiredPorts();

                switch (msg.MsgType)
                {
                    case PRSMessage.MESSAGE_TYPE.REQUEST_PORT:
                        {

                            //try to reserve requested port and send requested port back in response
                            response = RequestPort(msg.ServiceName);
                        }
                        break;

                    case PRSMessage.MESSAGE_TYPE.KEEP_ALIVE:
                        {
                            // client has requested that we keep their port alive
                            // find the reserved port by port# and service name
                            PortReservation reservation = ports.FirstOrDefault(p => !p.Available && p.ServiceName == msg.ServiceName && p.Port == msg.Port);
                            
                            // if found, keep it alive and send SUCCESS else
                            if (reservation !=null)
                            {
                                reservation.KeepAlive();
                                response = new PRSMessage(PRSMessage.MESSAGE_TYPE.RESPONSE, reservation.ServiceName, reservation.Port, PRSMessage.STATUS.SUCCESS);
                            }
                            else
                            {
                                response = new PRSMessage(PRSMessage.MESSAGE_TYPE.RESPONSE, msg.ServiceName, 0, PRSMessage.STATUS.SERVICE_NOT_FOUND);
                            }
                        }
                        break;

                    case PRSMessage.MESSAGE_TYPE.CLOSE_PORT:
                        {
                            // client has requested that we close their port
                            // find the reserved port by port# and service name
                            PortReservation reservation = ports.FirstOrDefault(p => !p.Available && p.ServiceName == msg.ServiceName && p.Port == msg.Port);

                            // if found, keep it alive and send SUCCESS else
                              if (reservation != null)
                            {
                                reservation.Close();
                                response = new PRSMessage(PRSMessage.MESSAGE_TYPE.RESPONSE, msg.ServiceName, reservation.Port, PRSMessage.STATUS.SUCCESS);
                            }
                            else
                            {
                                response = new PRSMessage(PRSMessage.MESSAGE_TYPE.RESPONSE, msg.ServiceName, msg.Port, PRSMessage.STATUS.SERVICE_NOT_FOUND);
                            }
                        }
                        break;

                    case PRSMessage.MESSAGE_TYPE.LOOKUP_PORT:
                        {
                            // client wants to know the reserved port number for a named service
                            // find the port
                            PortReservation reservation = ports.FirstOrDefault(p => !p.Available && p.ServiceName == msg.ServiceName);

                            // if found, send port number back
                            // else, SERVICE_NOT_FOUND
                            if (reservation != null)
                            {
                                response = new PRSMessage(PRSMessage.MESSAGE_TYPE.RESPONSE, msg.ServiceName, reservation.Port, PRSMessage.STATUS.SUCCESS);
                            }
                            else
                            {
                                response = new PRSMessage(PRSMessage.MESSAGE_TYPE.RESPONSE, msg.ServiceName, msg.Port, PRSMessage.STATUS.SERVICE_NOT_FOUND);
                            }

                        }
                        break;

                    case PRSMessage.MESSAGE_TYPE.STOP:
                        {
                            // client is telling us to close the appliation down
                            // stop the PRS and return SUCCESS
                            stopped = true;
                            response = new PRSMessage(PRSMessage.MESSAGE_TYPE.RESPONSE, "", 0, PRSMessage.STATUS.SUCCESS);
                        }
                        break;
                }

                return response;
            }

        }

        static void Usage()
        {
            Console.WriteLine("usage: PRSServer [options]");
            Console.WriteLine("\t-p < service port >");
            Console.WriteLine("\t-s < starting client port number >");
            Console.WriteLine("\t-e < ending client port number >");
            Console.WriteLine("\t-t < keep alive time in seconds >");
        }

        static void Main(string[] args)
        {

            // defaults
            ushort SERVER_PORT = 30000;
            ushort STARTING_CLIENT_PORT = 40000;
            ushort ENDING_CLIENT_PORT = 40099;
            int KEEP_ALIVE_TIMEOUT = 10;

            try
            {
                // process command options
                // -p < service port >
                // -s < starting client port number >
                // -e < ending client port number >
                // -t < keep alive time in seconds >
                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "-p":
                            SERVER_PORT = Convert.ToUInt16(args[++i]);
                            break;
                        case "-s":
                            STARTING_CLIENT_PORT = Convert.ToUInt16(args[++i]);
                            break;
                        case "-e":
                            ENDING_CLIENT_PORT = Convert.ToUInt16(args[++i]);
                            break;
                        case "-t":
                            KEEP_ALIVE_TIMEOUT = Convert.ToInt16(args[++i]);
                            break;
                    }
                }

                if (STARTING_CLIENT_PORT <= SERVER_PORT || STARTING_CLIENT_PORT >= ENDING_CLIENT_PORT)
                {
                    throw new Exception("Invalid range: -p must be outside of -s to -e range and -e must be larger then -s");
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Invalid Command line Arguments");
                Console.WriteLine(ex);
                Usage();
            }
       
            
            // initialize the PRS server
            PRS prs = new PRS(STARTING_CLIENT_PORT, ENDING_CLIENT_PORT, KEEP_ALIVE_TIMEOUT);
           
            // create the socket for receiving messages at the server
            Socket listeningSocket = new Socket(SocketType.Dgram, ProtocolType.Udp);

            // bind the listening socket to the PRS server port
            listeningSocket.Bind(new IPEndPoint(IPAddress.Any, SERVER_PORT));
            
            // Process client messages

            while (!prs.Stopped)
            {
                EndPoint clientEndPoint = null;
                try
                {
                    // receive a message from a client
                    clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    PRSMessage msg = PRSMessage.ReceiveMessage(listeningSocket, ref clientEndPoint);
                    
                    // let the PRS handle the message
                    PRSMessage responMessage = prs.HandleMessage(msg);

                    // send response message back to client
                    responMessage.SendMessage(listeningSocket, clientEndPoint);
                }
                catch (Exception ex)
                {
                    // attempt to send a UNDEFINED_ERROR response to the client, if we know who that was
                    if (clientEndPoint != null)
                    {
                        PRSMessage errorMessage = new PRSMessage(PRSMessage.MESSAGE_TYPE.RESPONSE, "", 0, PRSMessage.STATUS.UNDEFINED_ERROR );
                    }
                }
            }

            // close the listening socket
            listeningSocket.Close();

            // wait for a keypress from the user before closing the console window
            Console.WriteLine("Press Enter to exit");
            Console.ReadKey();
        }
    }
}
