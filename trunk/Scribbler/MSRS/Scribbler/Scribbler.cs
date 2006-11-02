//------------------------------------------------------------------------------
// Scribbler.cs
//
//     This code was generated by the DssNewService tool.
//     
//      Ben Axelrod 08/28/2006
//
//------------------------------------------------------------------------------
using Microsoft.Ccr.Core;
using Microsoft.Dss.Core;
using Microsoft.Dss.Core.Attributes;
using Microsoft.Dss.ServiceModel.Dssp;
using Microsoft.Dss.ServiceModel.DsspServiceBase;
using System;
using System.IO;
using System.IO.Ports;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security.Permissions;
using xml = System.Xml;
using submgr = Microsoft.Dss.Services.SubscriptionManager;
using dssp = Microsoft.Dss.ServiceModel.Dssp;
using W3C.Soap;
using System.Text;


namespace IPRE.ScribblerBase
{
    
    [DisplayName("Scribbler Base")]
    [Description("The IPRE Scribbler Base Service")]
    [Contract(Contract.Identifier)]
    //[PermissionSet(SecurityAction.PermitOnly, Name="Execution")]
    public class ScribblerService : DsspServiceBase
    {
        /// <summary>
        /// The saved state file name
        /// </summary>
        private const string _configFile = "Scribbler.State.xml";

        /// <summary>
        /// The current state
        /// </summary>
        [InitialStatePartner(Optional = true, ServiceUri = _configFile)]
        private ScribblerState _state = null;

        /// <summary>
        /// internal com port management
        /// </summary>
        private ScribblerCom _scribblerCom = new ScribblerCom();
        
        /// <summary>
        /// internal port for sending data to scribbler
        /// </summary>
        private Port<SendScribblerCommand> _scribblerComPort = new Port<SendScribblerCommand>();

        /// <summary>
        /// main operations port
        /// </summary>
        [ServicePort("/scribbler", AllowMultipleInstances=false)]
        private ScribblerOperations _mainPort = new ScribblerOperations();

        /// <summary>
        /// Subscription manager partner
        /// </summary>
        [Partner(dssp.Partners.SubscriptionManagerString, Contract = submgr.Contract.Identifier, CreationPolicy = PartnerCreationPolicy.CreateAlways)]
        submgr.SubscriptionManagerPort subMgrPort = new submgr.SubscriptionManagerPort();

        //Timer to poll scribbler at minimum frequency
        private System.Timers.Timer PollTimer;
        private static int TimerDelay = 250;           //4 Hz

        /// <summary>
        /// Default Service Constructor
        /// </summary>
        public ScribblerService(DsspServiceCreationPort creationPort) : base(creationPort)
        {
			CreateSuccess();
        }

        /// <summary>
        /// Service Start
        /// </summary>
        protected override void Start()
        {
            if (_state == null)
            {
                //initialize state
                _state = new ScribblerState();
                _state.ComPort = 0;
                _state.RobotName = null;

                //motors initially stopped
                _state.MotorLeft = 100;
                _state.MotorRight = 100;

                //_state.LightLeftConfig = new SensorConfig();
                //_state.LightRightConfig = new SensorConfig();
                //_state.LightCenterConfig = new SensorConfig();

                SaveState(_state);
            }

            // Listen on the main port for requests and call the appropriate handler.
            Interleave mainInterleave = ActivateDsspOperationHandlers();

            // Publish the service to the local Node Directory
            DirectoryInsert();

            // display HTTP service Uri
            LogInfo(LogGroups.Console, "Service uri: ");

            //open Scribbler Communications port
            if (ConnectToScribbler())
            {

                // Listen for a single Serial port request with an acknowledgement
                Activate(Arbiter.ReceiveWithIterator<SendScribblerCommand>(false, _scribblerComPort, SendScribblerCommandHandler));


                //add custom handlers to interleave
                mainInterleave.CombineWith(new Interleave(
                    new TeardownReceiverGroup(),
                    new ExclusiveReceiverGroup(
                        Arbiter.ReceiveWithIterator<SetMotor>(true, _mainPort, SetMotorHandler),
                        Arbiter.ReceiveWithIterator<SetLED>(true, _mainPort, SetLEDHandler),
                        Arbiter.ReceiveWithIterator<PlayTone>(true, _mainPort, PlayToneHandler),
                        //Arbiter.ReceiveWithIterator<ConfigureSensor>(true, _mainPort, ConfigureSensorHandler),
                        Arbiter.ReceiveWithIterator<SetName>(true, _mainPort, SetNameHandler)
                    ),
                    new ConcurrentReceiverGroup()
                ));


                PollTimer = new System.Timers.Timer();
                PollTimer.Interval = TimerDelay;
                PollTimer.AutoReset = true;
                PollTimer.Elapsed += new System.Timers.ElapsedEventHandler(PollTimer_Elapsed);
                PollTimer.Start();


                //play startup tone
                PlayToneBody startTone = new PlayToneBody(200, 1000, 2000);
                PlayTone sendcmd = new PlayTone();
                sendcmd.Body = startTone;
                _mainPort.Post(sendcmd);
            }

        }


        /// <summary>
        /// This will poll the scribbler at a minimum frequency
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PollTimer_Elapsed(object sender, EventArgs e)
        {
            ScribblerCommand cmd = new ScribblerCommand((byte)ScribblerHelper.Commands.GET_ALL);
            SendScribblerCommand sendcmd = new SendScribblerCommand(cmd);
            _scribblerComPort.Post(sendcmd);
        }


        private IEnumerator<ITask> SetNameHandler(SetName command)
        {
            if (command.Body == null || string.IsNullOrEmpty(command.Body.NewName))
            {
                command.ResponsePort.Post(new Fault());
                yield break;
            }

            string shortenedname;
            if (command.Body.NewName.Length > 8)
                shortenedname = command.Body.NewName.Substring(0, 8);
            else
                shortenedname = command.Body.NewName;

            _state.RobotName = shortenedname;

            ScribblerCommand cmd = new ScribblerCommand((byte)ScribblerHelper.Commands.SET_NAME, shortenedname);
            SendScribblerCommand sendcmd = new SendScribblerCommand(cmd);
            _scribblerComPort.Post(sendcmd);

            yield return Arbiter.Receive<ScribblerResponse>(false, sendcmd.ResponsePort,
                delegate(ScribblerResponse response)
                {
                    SaveState(_state);
                }
            );

            //reply to sender
            command.ResponsePort.Post(DefaultUpdateResponseType.Instance);

            yield break;
        }


        //private IEnumerator<ITask> ConfigureSensorHandler(ConfigureSensor command)
        //{
        //    if (command.Body == null)
        //    {
        //        command.ResponsePort.Post(new Fault());
        //        yield break;
        //    }

        //    if (command.Body.Sensor == null || command.Body.Configuration == null)
        //    {
        //        command.ResponsePort.Post(new Fault());
        //        yield break;
        //    }

        //    switch (command.Body.Sensor.ToUpper())
        //    {
        //        case "LEFT":
        //            _state.LightLeftConfig = command.Body.Configuration;
        //        break;

        //        case "CENTER":
        //            _state.LightCenterConfig = command.Body.Configuration;
        //        break;

        //        case "RIGHT":
        //            _state.LightRightConfig = command.Body.Configuration;
        //        break;
        //    }

        //    command.ResponsePort.Post(DefaultUpdateResponseType.Instance);
        //    yield break;
        //}


        /// <summary>
        /// Send a command to the Scribbler and wait for a response.
        /// </summary>
        /// <param name="ready"></param>
        /// <param name="legoCommand"></param>
        /// <returns></returns>
        private IEnumerator<ITask> SendScribblerCommandHandler(SendScribblerCommand command)
        {
            // Send command to robot and wait for echo and response
            ScribblerResponse validResponse = _scribblerCom.SendCommand(command.Body);
            if (validResponse == null)
            {
                LogError(LogGroups.Console, "Send Scribbler Command null response");
                command.ResponsePort.Post(new Fault());
            }
            //else if (validResponse.GetType() == typeof(LegoResponseException))
            //{
            //    // Pull exception text from response
            //    string errorMessage = "LEGO command: " + command.Body.LegoCommandCode.ToString() + " response generated an error: " + ((LegoResponseException)validResponse).ErrorMessage;
            //    command.ResponsePort.Post(new SoapFaultContext(new InvalidOperationException(errorMessage)).SoapFault);
            //}
            else
            {
                // Check to see if we need to update state
                // based on the response received from LEGO.
            //    PortSet<DefaultUpdateResponseType, Fault> responsePort = UpdateCurrentState(validResponse);
            //    if (responsePort != null)
            //    {
            //        yield return Arbiter.Choice(responsePort,
            //            delegate(DefaultUpdateResponseType response) { },
            //            delegate(Fault fault)
            //            {
            //                LogError(LogGroups.Console, "Failed to update LEGO NXT service state", fault);
            //            });
            //    }

            //    PostCommandProcessing(validResponse);

                //reset timer
                PollTimer.Enabled = false; 
                PollTimer.Enabled = true;

                //Update our state with the scribbler's response
                UpdateState(validResponse);

                command.ResponsePort.Post(validResponse);
            }

            // Ready to process another command
            Activate(Arbiter.ReceiveWithIterator<SendScribblerCommand>(false, _scribblerComPort, SendScribblerCommandHandler));
            yield break;
        }



        private bool ConnectToScribbler()
        {
            try
            {
                _state.Connected = false;
    
                //look for scribbler on last known Com port
                if (_state.ComPort != 0)
                {
                    _state.Connected = _scribblerCom.Open(_state.ComPort);
                }
                
                //scan all ports for the name of our Robot
                if (_state.Connected == false)
                {
                    _state.Connected = _scribblerCom.FindRobot(_state.RobotName);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogError(ex);
            }
            catch (IOException ex)
            {
                LogError(ex);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                LogError(ex);
            }
            catch (ArgumentException ex)
            {
                LogError(ex);
            }
            catch (InvalidOperationException ex)
            {
                LogError(ex);
            }

            if (!_state.Connected)
            {
                LogError(LogGroups.Console, "No Scribbler robot found.");
            }

            if (_state.Connected)
            {
                _state.RobotName = _scribblerCom.foundRobotName;
                _state.ComPort = _scribblerCom.openedComPort;
                LogInfo(LogGroups.Console, "Now connected to robot \"" + _state.RobotName + "\" on COM" + _state.ComPort);
                SaveState(_state);
            }

            return _state.Connected;
        }


        /// <summary>
        /// Handles incoming play tone requests
        /// </summary>
        private IEnumerator<ITask> PlayToneHandler(PlayTone message)
        {
            ScribblerCommand cmd = new ScribblerCommand((byte)ScribblerHelper.Commands.SET_SPEAKER_2,
                                                        message.Body.Duration,
                                                        message.Body.Frequency1,
                                                        message.Body.Frequency2);

            SendScribblerCommand sendcmd = new SendScribblerCommand(cmd);
            _scribblerComPort.Post(sendcmd);

            yield return Arbiter.Receive<ScribblerResponse>(false, sendcmd.ResponsePort,
                delegate(ScribblerResponse response)
                {

                }
            );

            //reply to sender
            message.ResponsePort.Post(DefaultUpdateResponseType.Instance);

            yield break;
        }


        /// <summary>
        /// Handles incoming SetLED requests
        /// </summary>
        private IEnumerator<ITask> SetLEDHandler(SetLED message)
        {
            ScribblerCommand cmd;

            switch (message.Body.LED)
            {
                case 0: //left LED
                    if (message.Body.State)
                        cmd = new ScribblerCommand((byte)ScribblerHelper.Commands.SET_LED_LEFT_ON);
                    else
                        cmd = new ScribblerCommand((byte)ScribblerHelper.Commands.SET_LED_LEFT_OFF);
                    break;
                case 1: //center LED
                    if (message.Body.State)
                        cmd = new ScribblerCommand((byte)ScribblerHelper.Commands.SET_LED_CENTER_ON);
                    else
                        cmd = new ScribblerCommand((byte)ScribblerHelper.Commands.SET_LED_CENTER_OFF);
                    break;
                case 2: //right LED
                    if (message.Body.State)
                        cmd = new ScribblerCommand((byte)ScribblerHelper.Commands.SET_LED_RIGHT_ON);
                    else
                        cmd = new ScribblerCommand((byte)ScribblerHelper.Commands.SET_LED_RIGHT_OFF);
                    break;
                case 3: //all LEDs
                    if (message.Body.State)
                        cmd = new ScribblerCommand((byte)ScribblerHelper.Commands.SET_LED_ALL_ON);
                    else
                        cmd = new ScribblerCommand((byte)ScribblerHelper.Commands.SET_LED_ALL_OFF);
                    break;
                default:
                    LogError("LED number set incorrect");
                    cmd = new ScribblerCommand();
                    break;
            }

            SendScribblerCommand sendcmd = new SendScribblerCommand(cmd);
            _scribblerComPort.Post(sendcmd);

            yield return Arbiter.Receive<ScribblerResponse>(false, sendcmd.ResponsePort,
                delegate(ScribblerResponse response)
                {

                }
            );

            //reply to sender
            message.ResponsePort.Post(DefaultUpdateResponseType.Instance);
            yield break;
        }

        int RequestPending = 0;

        /// <summary>
        /// Handles incoming set motor messages
        /// </summary>
        private IEnumerator<ITask> SetMotorHandler(SetMotor message)
        {
            if (message.Body == null)
            {
                message.ResponsePort.Post(new Fault());
                yield break;
            }
            if (message.Body.Motor == null)
            {
                message.ResponsePort.Post(new Fault());
                yield break;
            }

            // Requests come too fast, so dump ones that come in too fast.
            if (RequestPending > 0 && message.Body.Speed != 100)
            {
                message.ResponsePort.Post(new DefaultUpdateResponseType());
                yield break;
            }

            RequestPending++;

            if (message.Body.Motor.ToUpper().Contains("LEFT"))
            {
                _state.MotorLeft = message.Body.Speed;
            }
            else if (message.Body.Motor.ToUpper().Contains("RIGHT"))
            {
                _state.MotorRight = message.Body.Speed;
            }
            else
            {
                LogError("Motor name not set properly");
            }


            ScribblerCommand cmd = new ScribblerCommand((byte)ScribblerHelper.Commands.SET_MOTORS, (byte)_state.MotorRight, (byte)_state.MotorLeft);
            SendScribblerCommand sendcmd = new SendScribblerCommand(cmd);
            _scribblerComPort.Post(sendcmd);

            yield return Arbiter.Receive<ScribblerResponse>(false, sendcmd.ResponsePort,
                delegate(ScribblerResponse response)
                {
                    //if (response.Data[0] != (byte)ScribblerHelper.Commands.SET_MOTORS)
                    //    LogError("SetMotor picked up a wrong echo");

                    //initialize notify list
                    List<string> notify = new List<string>();
                    notify.Add("MOTORS");

                    // notify general subscribers
                    subMgrPort.Post(new submgr.Submit(_state, dssp.DsspActions.ReplaceRequest));

                    // notify selective subscribers
                    submgr.Submit sub = new submgr.Submit(_state, dssp.DsspActions.ReplaceRequest, notify.ToArray());
                    subMgrPort.Post(sub);

                    //reply to say we are done
                    message.ResponsePort.Post(DefaultUpdateResponseType.Instance);

                    RequestPending--;
                }
            );

            yield break;
        }


        /// <summary>
        /// Update state after recieving return data from robot
        /// </summary>
        /// <param name="response"></param>
        private void UpdateState(ScribblerResponse response)
        {
            //initialize notify list
            List<string> notify = new List<string>();

            switch ((ScribblerHelper.Commands)response.CommandType)
            {
                case ScribblerHelper.Commands.GET_STATE:
                    ScribblerHelper.GetStatusDecomp parse_get_state = new ScribblerHelper.GetStatusDecomp(response.Data[0], response.Data[1]);
                    if (_state.Stall != parse_get_state.Stall)
                    {
                        notify.Add("STALL");
                        _state.Stall = parse_get_state.Stall;
                    }
                    if (_state.LineLeft != parse_get_state.LineLeft)
                    {
                        notify.Add("LINELEFT");
                        _state.LineLeft = parse_get_state.LineLeft;
                    }
                    if (_state.LineRight != parse_get_state.LineRight)
                    {
                        notify.Add("LINERIGHT");
                        _state.LineRight = parse_get_state.LineRight;
                    }
                    _state.LEDLeft = parse_get_state.LedLeft;
                    _state.LEDCenter = parse_get_state.LedCenter;
                    _state.LEDRight = parse_get_state.LedRight;
                    break;
                case ScribblerHelper.Commands.GET_OPEN_LEFT:
                    bool New_Get_Open_Left = response.Data[0] == 1;             //NOTE: Not inverting logic here
                    if (_state.IRLeft != New_Get_Open_Left)
                    {
                        notify.Add("IRLEFT");
                        _state.IRLeft = New_Get_Open_Left;
                    }
                    break;
                case ScribblerHelper.Commands.GET_OPEN_RIGHT:
                    bool New_Get_Open_Right = response.Data[0] == 1;             //NOTE: Not inverting logic here
                    if (_state.IRRight != New_Get_Open_Right)
                    {
                        notify.Add("IRRIGHT");
                        _state.IRRight = New_Get_Open_Right;
                    }
                    break;
                case ScribblerHelper.Commands.GET_STALL:
                    bool New_Get_Stall = response.Data[0] == 1;
                    if (_state.Stall != New_Get_Stall)
                    {
                        notify.Add("STALL");
                        _state.Stall = New_Get_Stall;
                    }
                    break;
                case ScribblerHelper.Commands.GET_LIGHT_LEFT:
                    _state.LightLeft = BitConverter.ToUInt16(new byte[] { response.Data[1], response.Data[0] }, 0);
                    //if (_state.LightLeftConfig.GreaterThan && (_state.LightLeft > _state.LightLeftConfig.Threshold))
                        notify.Add("LIGHTLEFT");
                    break;
                case ScribblerHelper.Commands.GET_LIGHT_CENTER:
                    _state.LightCenter = BitConverter.ToUInt16(new byte[] { response.Data[1], response.Data[0] }, 0);
                    //if (_state.LightCenterConfig.GreaterThan && (_state.LightCenter > _state.LightCenterConfig.Threshold))
                        notify.Add("LIGHTCENTER");
                    break;
                case ScribblerHelper.Commands.GET_LIGHT_RIGHT:
                    _state.LightRight = BitConverter.ToUInt16(new byte[] { response.Data[1], response.Data[0] }, 0);
                    //if (_state.LightRightConfig.GreaterThan && (_state.LightRight > _state.LightRightConfig.Threshold))
                        notify.Add("LIGHTRIGHT");
                    break;
                case ScribblerHelper.Commands.GET_LINE_RIGHT:
                    bool New_Get_Line_Right = response.Data[0] == 1;
                    if (_state.LineRight != New_Get_Line_Right)
                    {
                        notify.Add("LINERIGHT");
                        _state.LineRight = New_Get_Line_Right;
                    }
                    break;
                case ScribblerHelper.Commands.GET_LINE_LEFT:
                    bool New_Get_Line_Left = response.Data[0] == 1;
                    if (_state.LineLeft != New_Get_Line_Left)
                    {
                        notify.Add("LINELEFT");
                        _state.LineLeft = New_Get_Line_Left;
                    }
                    break;
                case ScribblerHelper.Commands.GET_NAME:
                    Encoding.ASCII.GetChars(response.Data, 0, 8);
                    break;
                case ScribblerHelper.Commands.GET_LIGHT_ALL:
                    _state.LightLeft = BitConverter.ToUInt16(new byte[] { response.Data[1], response.Data[0] }, 0);
                    _state.LightCenter = BitConverter.ToUInt16(new byte[] { response.Data[3], response.Data[2] }, 0);
                    _state.LightRight = BitConverter.ToUInt16(new byte[] { response.Data[5], response.Data[4] }, 0);
                    //if (_state.LightLeftConfig.GreaterThan && (_state.LightLeft > _state.LightLeftConfig.Threshold))
                        notify.Add("LIGHTLEFT");
                    //if (_state.LightCenterConfig.GreaterThan && (_state.LightCenter > _state.LightCenterConfig.Threshold))
                        notify.Add("LIGHTCENTER");
                    //if (_state.LightRightConfig.GreaterThan && (_state.LightRight > _state.LightRightConfig.Threshold))
                        notify.Add("LIGHTRIGHT");
                    break;
                case ScribblerHelper.Commands.GET_IR_ALL:
                    bool newirleft = response.Data[0] == 1;    //NOTE: Not inverting logic here
                    bool newirright = response.Data[1] == 1;
                    if (_state.IRLeft != newirleft)
                    {
                        notify.Add("IRLEFT");
                        _state.IRLeft = newirleft;
                    }
                    if (_state.IRRight != newirright)
                    {
                        notify.Add("IRRIGHT");
                        _state.IRRight = newirright;
                    }
                    break;
                case ScribblerHelper.Commands.GET_LINE_ALL:
                    bool newlineleft = response.Data[0] == 1;
                    bool newlineright = response.Data[1] == 1;
                    if (_state.LineLeft != newlineleft)
                    {
                        notify.Add("LINELEFT");
                        _state.LineLeft = newlineleft;
                    }
                    if (_state.LineRight != newlineright)
                    {
                        notify.Add("LINERIGHT");
                        _state.LineRight = newlineright;
                    }
                    break;
                case ScribblerHelper.Commands.GET_ALL:
                    bool New_Get_All_IR_Left = response.Data[0] == 1;    //NOTE: Not inverting logic here
                    bool New_Get_All_IR_Right = response.Data[1] == 1;
                    if (_state.IRLeft != New_Get_All_IR_Left)
                    {
                        notify.Add("IRLEFT");
                        _state.IRLeft = New_Get_All_IR_Left;
                    }
                    if (_state.IRRight != New_Get_All_IR_Right)
                    {
                        notify.Add("IRRIGHT");
                        _state.IRRight = New_Get_All_IR_Right;
                    }

                    _state.LightLeft = BitConverter.ToUInt16(new byte[] { response.Data[3], response.Data[2] }, 0);
                    _state.LightCenter = BitConverter.ToUInt16(new byte[] { response.Data[5], response.Data[4] }, 0);
                    _state.LightRight = BitConverter.ToUInt16(new byte[] { response.Data[7], response.Data[6] }, 0);
                    //if (_state.LightLeftConfig.GreaterThan && (_state.LightLeft > _state.LightLeftConfig.Threshold))
                        notify.Add("LIGHTLEFT");
                    //if (_state.LightCenterConfig.GreaterThan && (_state.LightCenter > _state.LightCenterConfig.Threshold))
                        notify.Add("LIGHTCENTER");
                    //if (_state.LightRightConfig.GreaterThan && (_state.LightRight > _state.LightRightConfig.Threshold))
                        notify.Add("LIGHTRIGHT");

                    bool New_Get_All_Line_Left = response.Data[8] == 1;
                    bool New_Get_All_Line_Right = response.Data[9] == 1;
                    if (_state.LineLeft != New_Get_All_Line_Left)
                    {
                        notify.Add("LINELEFT");
                        _state.LineLeft = New_Get_All_Line_Left;
                    }
                    if (_state.LineRight != New_Get_All_Line_Right)
                    {
                        notify.Add("LINERIGHT");
                        _state.LineRight = New_Get_All_Line_Right;
                    }

                    bool newstall = response.Data[10] == 1;
                    if (_state.Stall != newstall)
                    {
                        notify.Add("STALL");
                        _state.Stall = newstall;
                    }
                    break;
                case ScribblerHelper.Commands.GET_ALL_BINARY:
                case ScribblerHelper.Commands.SET_MOTORS_OFF:
                case ScribblerHelper.Commands.SET_MOTORS:
                case ScribblerHelper.Commands.SET_LED_LEFT_ON:
                case ScribblerHelper.Commands.SET_LED_LEFT_OFF:
                case ScribblerHelper.Commands.SET_LED_CENTER_ON:
                case ScribblerHelper.Commands.SET_LED_CENTER_OFF:
                case ScribblerHelper.Commands.SET_LED_RIGHT_ON:
                case ScribblerHelper.Commands.SET_LED_RIGHT_OFF:
                case ScribblerHelper.Commands.SET_SPEAKER:
                case ScribblerHelper.Commands.SET_SPEAKER_2:
                case ScribblerHelper.Commands.SET_NAME:
                case ScribblerHelper.Commands.SET_LED_ALL_ON:
                case ScribblerHelper.Commands.SET_LED_ALL_OFF:
                case ScribblerHelper.Commands.SET_LOUD:
                case ScribblerHelper.Commands.SET_QUIET:
                    ScribblerHelper.AllBinaryDecomp parse_all_binary = new ScribblerHelper.AllBinaryDecomp(response.Data[0]);

                    if (_state.IRLeft != parse_all_binary.IRLeft)
                    {
                        notify.Add("IRLEFT");
                        _state.IRLeft = parse_all_binary.IRLeft;       //NOTE: Not inverting logic here
                    }
                    if (_state.IRRight != parse_all_binary.IRRight)
                    {
                        notify.Add("IRRIGHT");
                        _state.IRRight = parse_all_binary.IRRight;
                    }

                    if (_state.LineLeft != parse_all_binary.LineLeft)
                    {
                        notify.Add("LINELEFT");
                        _state.LineLeft = parse_all_binary.LineLeft;
                    }
                    if (_state.LineRight != parse_all_binary.LineRight)
                    {
                        notify.Add("LINERIGHT");
                        _state.LineRight = parse_all_binary.LineRight;
                    }

                    if (_state.Stall != parse_all_binary.Stall)
                    {
                        notify.Add("STALL");
                        _state.Stall = parse_all_binary.Stall;
                    }
                    break;
                default:
                    LogError(LogGroups.Console, "Update State command missmatch");
                    break;
            }

            // notify general subscribers
            subMgrPort.Post(new submgr.Submit(_state, dssp.DsspActions.ReplaceRequest));

            // notify selective subscribers
            submgr.Submit sub = new submgr.Submit(_state, dssp.DsspActions.ReplaceRequest, notify.ToArray());
            subMgrPort.Post(sub);
        }



        /// <summary>
        /// Get Handler
        /// </summary>
        [ServiceHandler(ServiceHandlerBehavior.Concurrent)]
        public virtual IEnumerator<ITask> GetHandler(Get get)
        {
            //SetNameBody newname = new SetNameBody();
            //newname.NewName = "Benj";
            //SetName b = new SetName();
            //b.Body = newname;

            //_mainPort.Post(b);

            //_scribblerComm.SendCommand(new ScribblerCommand((byte)ScribblerHelper.Commands.GET_ALL_BINARY));
            
            //SetMotor message = new SetMotor();
            //if (reverse)
            //{
            //    message.Body.Motor = "Left";
            //    message.Body.Speed = 1;
            //    reverse = false;
            //}
            //else
            //{
            //    message.Body.Motor = "Left";
            //    message.Body.Speed = 200;
            //    reverse = true;
            //}

            //_mainPort.Post(message);

            get.ResponsePort.Post(_state);
            yield break;
        }

        /// <summary>
        /// Replace Handler
        /// </summary>
        [ServiceHandler(ServiceHandlerBehavior.Exclusive)]
        public virtual IEnumerator<ITask> ReplaceHandler(Replace replace)
        {
            _state = replace.Body;
            replace.ResponsePort.Post(DefaultReplaceResponseType.Instance);
            yield break;
        }


        // General Subscription
        [ServiceHandler(ServiceHandlerBehavior.Exclusive)]
        public IEnumerator<ITask> SubscribeHandler(Subscribe subscribe)
        {
            base.SubscribeHelper(subMgrPort, subscribe.Body, subscribe.ResponsePort);
            yield break;
        }

        // Custom Subscription
        [ServiceHandler(ServiceHandlerBehavior.Exclusive)]
        public IEnumerator<ITask> SelectiveSubscribeHandler(SelectiveSubscribe subRequest)
        {
            submgr.InsertSubscription selectiveSubscription = new submgr.InsertSubscription(
                new submgr.InsertSubscriptionMessage(
                    subRequest.Body.Subscriber,
                    subRequest.Body.Expiration,
                    0));

            selectiveSubscription.Body.NotificationCount = subRequest.Body.NotificationCount;

            List<submgr.QueryType> subscribeFilter = new List<submgr.QueryType>();

            //items in this loop are OR'ed together in the subscription
            foreach (string s in subRequest.Body.Sensors)
            {
                LogInfo("Adding subscription for: " + s.ToUpper());

                //you can achieve an AND behavior by adding a list of strings in the new QueryType
                subscribeFilter.Add(new submgr.QueryType(s.ToUpper()));
            }

            selectiveSubscription.Body.QueryList = subscribeFilter.ToArray();
            subMgrPort.Post(selectiveSubscription);

            yield return Arbiter.Choice(selectiveSubscription.ResponsePort,
                delegate(dssp.SubscribeResponseType response)
                {
                    subRequest.ResponsePort.Post(response);
                },
                delegate(Fault fault)
                {
                    subRequest.ResponsePort.Post(fault);
                });
            yield break;
        }


    }
}
