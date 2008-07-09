//-----------------------------------------------------------------------
//  
//     
//      Ben Axelrod 08/28/2006
//
//-----------------------------------------------------------------------

//#define DEBUG
#undef DEBUG

using System;

using System.Text;
using System.IO;
using System.IO.Ports;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using Microsoft.Dss.Core.Attributes;

using Microsoft.Ccr.Core;
using Microsoft.Dss.ServiceModel.DsspServiceBase;
using System.Threading;


namespace Myro.Services.Scribbler.ScribblerBase
{
    internal class ScribblerCom
    {
        private SerialPort _serialPort = null;

        private const int _baudRate = 38400;

        private ScribblerHelper helper = new ScribblerHelper();

        /// <summary>
        /// after this number of milliseconds without seeing data, the scribbler will send its 'find me' message
        /// </summary>
        private const int noCommsTimeout = 1000;

        /// <summary>
        /// After this number of milliseconds while waiting for known data to arrive, 
        /// the service will give up.
        /// <remarks>NOTE: The timer gets reset after every packet received.
        /// Messages to and from scribbler take about 70 ms.
        /// Keep in mind the base rate at which the service asks for refreshed sensor data (250 ms)</remarks>
        /// </summary>
        //private const int ReadTimeOut = 120;

        /// <summary>
        /// the scribbler's 'find me' message must contain this string
        /// </summary>
        //private const string characteristicString = "IPRE";
        private const string characteristicString = "Scribbler";

        //internal ScribblerDataPort ScribblerComInboundPort = null;


        //these are just to transfer back to the main scribbler service
        public string foundRobotName;
        public int openedComPort;

        /// <summary>
        /// Open a serial port.
        /// </summary>
        /// <param name="comPort"></param>
        /// <param name="baudRate"></param>
        /// <returns>A Ccr Port for receiving serial port data</returns>
        internal bool Open(int comPort)
        {
            if (_serialPort != null)
                Close();

            //debug
#if DEBUG
            Console.WriteLine("Opening com port: " + comPort);
#endif

            _serialPort = new SerialPort("COM" + comPort.ToString(System.Globalization.NumberFormatInfo.InvariantInfo), _baudRate);
            _serialPort.Encoding = Encoding.Default;
            _serialPort.Parity = Parity.None;
            _serialPort.DataBits = 8;
            _serialPort.StopBits = StopBits.One;
            _serialPort.ReadTimeout = 500;
            _serialPort.WriteTimeout = 500;

            try
            {
                string name = TrySerialPort(_serialPort.PortName);

                if (name != null)
                {
                    System.Threading.Thread.Sleep(500); //give the BT device some time between closing and opening of the port
                    _serialPort.Open(); 
                    foundRobotName = name;
                    openedComPort = comPort;
                }
                else
                    return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Invalid Serial Port.");

#if DEBUG
                Console.WriteLine("Open caught exception: " + ex);
                throw new Exception("TrySerialPort caught exception", ex);
#endif

                return false;
            }
            return true;
        }


        /// <summary>
        /// Attempt to read our data from a serial port.
        /// </summary>
        /// <param name="spName">Serial port name</param>
        /// <returns>name of robot attached to port</returns>
        private string TrySerialPort(string spName)
        {
#if DEBUG
            Console.WriteLine("Trying Serial Port: " + spName); //DEBUG
#endif

            SerialPort p = null;
            string robotname = null;

            try
            {
                p = new SerialPort(spName, _baudRate, Parity.None, 8, StopBits.One);
                p.Handshake = Handshake.None;
                p.Encoding = Encoding.ASCII;
                p.Open();
                _serialPort = p;

#if DEBUG
                Console.WriteLine("reading once"); //DEBUG
#endif

                // Send the GETINFO string
                ScribblerResponse srp = SendCommand(new ScribblerCommand((byte)ScribblerHelper.Commands.GET_INFO));

                // GTEMP: Send twice - some problem with dongle
                srp = SendCommand(new ScribblerCommand((byte)ScribblerHelper.Commands.GET_INFO));

                UTF8Encoding enc = new UTF8Encoding();
                string s = enc.GetString(srp.Data);

                if (s.Length == 0)
                {
                    System.Threading.Thread.Sleep((int)(noCommsTimeout * 1.5));
#if DEBUG
                    Console.WriteLine("reading again"); //DEBUG
#endif
                    s = p.ReadExisting(); //try again
                }

                if (s.Length == 0)
                {
#if DEBUG
                    Console.WriteLine("length == 0"); //DEBUG
#endif
                    return null;
                }

                // we are receiving data.
#if DEBUG
                Console.WriteLine("we are receiving data."); //DEBUG
                Console.WriteLine("received: \"" + s + "\"");
#endif

                int index = s.IndexOf(characteristicString);
                //not a Scribbler robot
                if (index < 0)
                {
#if DEBUG
                    Console.WriteLine("not a Scribbler robot."); //DEBUG
#endif
                    _serialPort = null;
                    return null;
                }

                // Now get robotname
                srp = SendCommand(new ScribblerCommand((byte)ScribblerHelper.Commands.GET_NAME));
                enc = new UTF8Encoding();
                robotname = enc.GetString(srp.Data);
                if (robotname.Length == 0)
                {
#if DEBUG
                    Console.WriteLine("Cannot get name"); //DEBUG
#endif
                    robotname = "Noname";
                }

                // Sending Echo off command
                SendCommand(new ScribblerCommand((byte)ScribblerHelper.Commands.SET_ECHO_MODE, (byte)1, (byte)1));

#if DEBUG
                Console.WriteLine("TrySerialPort found: " + robotname); //DEBUG
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine("TrySerialPort caught exception: " + ex);
                //throw new Exception("TrySerialPort caught exception", ex);
#endif
            }
            finally
            {
                if (p != null && p.IsOpen)
                    p.Close();
                p.Dispose();
            }

            return robotname;
        }


        /// <summary>
        /// Attempt to find and open a Scribbler on any serial port.
        /// <returns>True if a robot unit was found</returns>
        /// </summary>
        public bool FindRobot(string robotname)
        {
            //debug
            //Console.WriteLine("FindRobot: " + robotname);

            //if there are multiple robots connected, add to list and prompt user
            //item0 = COM port name
            //item1 = robot name
            List<Tuple<String, String>> foundRobots = new List<Tuple<string, string>>();

            foreach (string spName in SerialPort.GetPortNames())
            {
                Console.WriteLine("Checking " + spName);
                string spName2 = FixComPortName(spName);

                Console.Write("Checking for robot on " + spName2 + ".  ");

                string tempName = TrySerialPort(spName2);

                if (tempName != null)
                {
                    Console.Write("Found robot \"" + tempName + "\"\n");

                    if (tempName == robotname)
                    {
                        return Open(int.Parse(spName2.Substring(3, spName2.Length - 3)));
                    }
                    else
                    {
                        Tuple<String, String> pair = new Tuple<string, string>(spName2, tempName);
                        foundRobots.Add(pair);
                    }
                }
                else
                    Console.Write("\n");
            }

            //only one robot found. so connect
            if (foundRobots.Count == 1)
            {
                return Open(int.Parse(foundRobots[0].Item0.Substring(3, foundRobots[0].Item0.Length - 3)));
            }
            //many robots found. prompt user to connect
            else if (foundRobots.Count > 0)
            {
                Console.WriteLine("*** Found multiple robots: ***");
                foreach (Tuple<string, string> tup in foundRobots)
                {
                    Console.WriteLine("   Robot \"" + tup.Item1 + "\" on " + tup.Item0);
                }


                bool selected = false;
                while (!selected)
                {
                    Console.WriteLine("Which robot would you like to connect to?");
                    Console.Write("Enter the robot's name or COM port:");

                    string connect = Console.ReadLine();
                    connect = connect.ToUpper();          //read robot name and standardize

                    int connectPort = -1;
                    int.TryParse(connect, out connectPort); //convert to int if possible

                    //if user entered number
                    if (connectPort > 0)
                        return Open(connectPort);

                    //NOTE: Item0 = COMport, Item1 = RobotName
                    foreach (Tuple<string, string> tup in foundRobots)
                    {
                        int foundPort = -1;
                        int.TryParse(tup.Item0.Substring(3, tup.Item0.Length - 3), out foundPort); //convert to int if possible

                        //if user entered name
                        if (connect == tup.Item1.ToUpper())
                            return Open(foundPort);
                        else if (connect == tup.Item0.ToUpper()) //if user entered COMport
                            return Open(foundPort);
                    }

                }
            }

            //no robots found
            return false;
        }


        void serialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            Console.WriteLine("serialPort_ErrorReceived: " + e);
            //throw new IOException();
        }


        /// <summary>
        /// DO NOT USE THIS COMMAND DIRECTLY.
        /// In Scribbler.cs, post a message to _scribblerComPort
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        internal ScribblerResponse SendCommand(ScribblerCommand cmd)
        {
            ScribblerResponse echo = null;
            ScribblerResponse response = null;
            int outMessageSize = helper.CommandSize((ScribblerHelper.Commands)cmd.CommandType);
            byte[] buffer = new byte[outMessageSize];

            if (buffer != null)
            {

                int ix = 0;

                //buffer = cmd.ToByteArray();

                buffer[ix++] = cmd.CommandType;

                // Changed this so it doesn't copy entire command (Fluke commands are shorter than 8 bytes)
                int len = Math.Min(cmd.Data.Length, (outMessageSize - 1));
                if (cmd.Data != null && cmd.Data.Length > 0)
                    Array.Copy(cmd.Data, 0, buffer, 1, len);
                ix += len;
                //foreach (byte b in cmd.Data)
                //    buffer[ix++] = b;


                //fill to standard size
                while (ix < outMessageSize)
                    buffer[ix++] = 0;


#if DEBUG
                Console.Write("\nSent: ");
                foreach (byte b in buffer)
                {
                    if (b != 0)
                        Console.Write(b + " ");
                    else
                        Console.Write("` ");
                }
                Console.Write("\n");
#endif


                //try
                //{
                // When requesting a response, clear the inbound buffer 
                if (_serialPort.BytesToRead > 0)
                    _serialPort.DiscardInBuffer();

                _serialPort.Write(buffer, 0, ix);
                //}
                //catch
                //{
                //Console.WriteLine("Serial Port Timeout.  Lost connection with Scribbler.");
                //throw new IOException();
                //}

                if (helper.HasEcho((ScribblerHelper.Commands)cmd.CommandType))
                    echo = GetEcho(buffer, outMessageSize);

                response = GetCommandResponse(helper.ReturnSize((ScribblerHelper.Commands)cmd.CommandType), helper.HasEcho((ScribblerHelper.Commands)cmd.CommandType));
            }
            return response;
        }



        /// <summary>
        /// Read Serial Port for echo
        /// </summary>
        /// <param name="outBuff">The outbound message to match</param>
        /// <returns>ScribblerResponse</returns>
        private ScribblerResponse GetEcho(byte[] outBuff, int echoSize)
        {
            byte[] inBuff = new byte[echoSize];
            ScribblerResponse response = null;
            int ixOutBuff = 0;
            DateTime lastbytetime = DateTime.Now;
            //try
            //{
            while (ixOutBuff < echoSize) // && Compare(DateTime.Now, lastbytetime) < ReadTimeOut)
            {
                byte[] temp = new byte[1];
                _serialPort.Read(temp, 0, 1); //get 1 byte
                if (temp[0] == outBuff[ixOutBuff])
                {
                    inBuff[ixOutBuff] = temp[0];
                    ixOutBuff++;
                    lastbytetime = DateTime.Now;
                }
                else
                {
                    Console.WriteLine("Echo missmatch");
                    break;
                }
            }

            response = new ScribblerResponse();
            response.Data = (byte[])inBuff.Clone();

#if DEBUG
                Console.Write("Echo: ");
                foreach (byte b in response.Data)
                {
                    if (b != 0)
                        Console.Write(b + " ");
                    else
                        Console.Write("` ");
                }
                Console.Write("\n");
#endif
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine("GetCommandResponse Exception: " + ex);
            //    //throw;
            //}
            return response;
        }

        /// <summary>
        /// compares the two times and returns number of milliseconds between the two.
        /// </summary>
        /// <param name="now">the larger time</param>
        /// <param name="then"></param>
        /// <returns>delta milliseconds</returns>
        private int Compare(DateTime now, DateTime then)
        {
            return (now.Second - then.Second) * 1000 + (now.Millisecond - then.Millisecond);
        }

        /// <summary>
        /// Read Serial Port for a number of bytes and put into ScribblerResponse
        /// </summary>
        /// <param name="nBytes">number of bytes to read (includes the command type byte)</param>
        /// <returns>ScribblerResponse</returns>
        private ScribblerResponse GetCommandResponse(int nBytes, bool cmdEcho)
        {
            //Console.WriteLine("GetCommandResponse: creating buffer");
            byte[] inBuff = new Byte[Math.Abs(nBytes)];

            //Console.WriteLine("Check 1");
            ScribblerResponse response = null;
            //Console.WriteLine("Check 2");
            int read = 0;
            bool error = false, done = false;
            //try
            //{
            while (read < Math.Abs(nBytes) && !done)
            {
                int canread;
                int count = 0;
                while (((canread = _serialPort.BytesToRead) == 0) && count++ < 5)
                    Thread.Sleep(50); //spin 

                if (count > 5)
                    break;

                if (nBytes < 0)
                {
                    //Console.WriteLine("Reading variable length (buffer size " + inBuff.Length + ")");
                    for (int i = 0; i < canread; i++)
                    {
                        _serialPort.Read(inBuff, read++, 1);
                        //Console.WriteLine("  Got " + inBuff[read - 1] + " at " + (read - 1));
                        if (inBuff[read - 1] == 0x0A)
                            done = true;
                    }
                }
                else
                {
                    int needtoread = nBytes - read;
                    //Console.WriteLine("Reading fixed length of " + needtoread + ", buffer " + nBytes);
                    if (canread > needtoread)
                    {
                        _serialPort.Read(inBuff, read, needtoread);
                        read += needtoread;
                    }
                    else
                    {
                        _serialPort.Read(inBuff, read, canread);
                        read += canread;
                    }
                }
            }


            int dataBytes = (cmdEcho ? Math.Abs(nBytes) - 1 : Math.Abs(nBytes));
            response = new ScribblerResponse(Math.Max(0, dataBytes));
            response.CommandType = (cmdEcho ? inBuff[inBuff.Length - 1] : (byte)0);
            Array.Copy(inBuff, response.Data, dataBytes);

#if DEBUG
                    Console.Write("Got: ");
                    foreach (byte b in response.Data)
                    {
                        if (b != 0)
                            Console.Write(b + " ");
                        else
                            Console.Write("` ");
                    }
                    Console.Write("\n");
#endif
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine("GetCommandResponse Exception: " + ex);
            //    //throw;
            //}
            return response;
        }



        /// <summary>
        /// Close the connection to a serial port.
        /// </summary>
        public void Close()
        {
            if (_serialPort != null)
            {
                if (_serialPort.IsOpen)
                    _serialPort.Close();
                _serialPort = null;
            }
        }



        /// <summary>
        /// Search serial port data for binary characters.  
        /// Unrecognized data indicates that we are 
        /// operating in binary mode or receiving data at 
        /// the wrong baud rate.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private static bool UnrecognizedData(string data)
        {
            foreach (char c in data)
            {
                if ((c < 32 || c > 126) && (c != 0))
                {
                    Console.WriteLine("unrecognized: " + c);
                    return true;
                }
            }
            return false;
        }

        private static string FixComPortName(string name)
        {
            char[] tmp = name.ToCharArray();
            if (name[name.Length - 1] < 48 || name[name.Length - 1] > 57)
            {
                //Console.WriteLine("Fixing name: " + name + " -> " + name.Substring(0, name.Length - 1)); //DEBUG
                return name.Substring(0, name.Length - 1);
            }
            return name;
        }


    }







}
