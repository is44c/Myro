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
using System.Collections.Generic;
using System.ComponentModel;
using System.Security.Permissions;
using xml = System.Xml;
using submgr = Microsoft.Dss.Services.SubscriptionManager;
using dssp = Microsoft.Dss.ServiceModel.Dssp;
using W3C.Soap;

namespace IPRE.ScribblerBase
{
    
    [DisplayName("Scribbler Base")]
    [Description("The IPRE Scribbler Base Service")]
    [Contract(Contract.Identifier)]
    //[PermissionSet(SecurityAction.PermitOnly, Name="Execution")]
    public class ScribblerService : DsspServiceBase
    {
        private ScribblerState _state = new ScribblerState();

        private ScribblerComm _scribblerComm;
        private ScribblerDataPort _scribblerComPort;
        
        [ServicePort("/scribbler", AllowMultipleInstances=false)]
        private ScribblerOperations _mainPort = new ScribblerOperations();

        // Subscription manager partner
        [Partner(dssp.Partners.SubscriptionManagerString, Contract = submgr.Contract.Identifier, CreationPolicy = PartnerCreationPolicy.CreateAlways)]
        submgr.SubscriptionManagerPort subMgrPort = new submgr.SubscriptionManagerPort();

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

            // Listen on the main port for requests and call the appropriate handler.
            Interleave mainInterleave = ActivateDsspOperationHandlers();

            // Publish the service to the local Node Directory
            DirectoryInsert();

            // display HTTP service Uri
            LogInfo(LogGroups.Console, "Service uri: ");


            //open Scribbler Communications port
            _scribblerComm = new ScribblerComm();
            _scribblerComPort = new ScribblerDataPort();
            _scribblerComPort = _scribblerComm.Open(6, 38400);

            //add custom handlers to interleave
            mainInterleave.CombineWith(new Interleave(
                new TeardownReceiverGroup(),
                new ExclusiveReceiverGroup(
                    Arbiter.ReceiveWithIterator<SensorNotification>(true, _scribblerComPort, SensorNotificationHandler),
                    Arbiter.ReceiveWithIterator<SetMotor>(true, _mainPort, SetMotorHandler),
                    Arbiter.ReceiveWithIterator<Ping>(true, _mainPort, PingHandler),
                    Arbiter.ReceiveWithIterator<SetLED>(true, _mainPort, SetLEDHandler),
                    Arbiter.ReceiveWithIterator<PlayTone>(true, _mainPort, PlayToneHandler)
                ),
                new ConcurrentReceiverGroup()
            ));

            //play startup tone
            _scribblerComm.SendCommand(new ScribblerCommand((byte)ScribblerHelper.Commands.SET_SPEAKER, 20, 100, 200));

        }

        /// <summary>
        /// Handles incoming ping requests
        /// </summary>
        private IEnumerator<ITask> PingHandler(Ping message)
        {
            _scribblerComm.SendCommand(new ScribblerCommand((byte)ScribblerHelper.Commands.GET_LINE_LEFT));

            yield return Arbiter.Receive<SensorNotification>(false, _scribblerComPort,
                delegate(SensorNotification response)
                {
                    if (response.Sensor != (byte)ScribblerHelper.Commands.GET_LINE_LEFT_RESPONSE)
                        LogError("Ping picked up a wrong reply");

                    //reply to sender
                    message.ResponsePort.Post(DefaultUpdateResponseType.Instance);

                }
            );

            yield break;
        }


        /// <summary>
        /// Handles incoming play tone requests
        /// </summary>
        private IEnumerator<ITask> PlayToneHandler(PlayTone message)
        {
            byte duration = (byte)(message.Body.Duration / 10);
            byte freq1 = (byte)((message.Body.Frequency1 - 250) / 6);
            byte freq2 = (byte)((message.Body.Frequency2 - 250) / 6);

            _scribblerComm.SendCommand(new ScribblerCommand((byte)ScribblerHelper.Commands.SET_SPEAKER, freq1, freq2));

            //reply to sender
            message.ResponsePort.Post(DefaultUpdateResponseType.Instance);

            yield break;
        }


        /// <summary>
        /// Handles incoming SetLED requests
        /// </summary>
        private IEnumerator<ITask> SetLEDHandler(SetLED message)
        {
            switch (message.Body.LED)
            {
                case 0:
                    if (message.Body.State)
                        _scribblerComm.SendCommand(new ScribblerCommand((byte)ScribblerHelper.Commands.SET_LED_LEFT_ON));
                    else
                        _scribblerComm.SendCommand(new ScribblerCommand((byte)ScribblerHelper.Commands.SET_LED_LEFT_OFF));
                    break;
                case 1:
                    if (message.Body.State)
                        _scribblerComm.SendCommand(new ScribblerCommand((byte)ScribblerHelper.Commands.SET_LED_CENTER_ON));
                    else
                        _scribblerComm.SendCommand(new ScribblerCommand((byte)ScribblerHelper.Commands.SET_LED_CENTER_OFF));
                    break;
                case 2:
                    if (message.Body.State)
                        _scribblerComm.SendCommand(new ScribblerCommand((byte)ScribblerHelper.Commands.SET_LED_RIGHT_ON));
                    else
                        _scribblerComm.SendCommand(new ScribblerCommand((byte)ScribblerHelper.Commands.SET_LED_RIGHT_OFF));
                    break;
                default:
                    LogError("LED number set incorrect");
                    break;
            }

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
            // Requests come too fast, so dump ones that come in too fast.
            if (RequestPending > 0)
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

            _scribblerComm.SendCommand(new ScribblerCommand((byte)ScribblerHelper.Commands.SET_MOTORS, (byte)_state.MotorRight, (byte)_state.MotorLeft));
            yield return Arbiter.Receive<ScribblerCommand>(false, _scribblerComPort,
                delegate(ScribblerCommand response)
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
        /// Handles incoming sensor messages
        /// </summary>
        private IEnumerator<ITask> SensorNotificationHandler(SensorNotification message)
        {
            //initialize notify list
            List<string> notify = new List<string>();

            //update state
            switch (ScribblerHelper.SensorType((byte)message.Sensor))
            {
                case "IRLeft":
                    _state.IRLeft = (message.Status > 0);
                    notify.Add("IRLEFT");
                    break;

                case "IRRight":
                    _state.IRRight = (message.Status > 0);
                    notify.Add("IRRIGHT");
                    break;

                case "Stall":
                    _state.Stall = (message.Status > 0);
                    notify.Add("STALL");
                    break;

                case "LineLeft":
                    _state.LineLeft = (message.Status > 0);
                    notify.Add("LINELEFT");
                    break;

                case "LineRight":
                    _state.LineRight = (message.Status > 0);
                    notify.Add("LINERIGHT");
                    break;

                case "LightLeft":
                    _state.LightLeft = message.Status;
                    notify.Add("LIGHTLEFT");
                    break;

                case "LightRight":
                    _state.LightRight = message.Status;
                    notify.Add("LIGHTRIGHT");
                    break;

                case "LightCenter":
                    _state.LightCenter = message.Status;
                    notify.Add("LIGHTCENTER");
                    break;

                    //only notify if there is a change
                case "AllBinary":
                    ScribblerHelper.AllBinaryDecomp newState = new ScribblerHelper.AllBinaryDecomp(message.Status);
                    if (newState.IRLeft != _state.IRLeft)
                    {
                        notify.Add("IRLEFT");
                        _state.IRLeft = newState.IRLeft;
                    }
                    if (newState.IRRight != _state.IRRight)
                    {
                        notify.Add("IRRIGHT");
                        _state.IRRight = newState.IRRight;
                    }
                    if (newState.Stall != _state.Stall)
                    {
                        notify.Add("STALL");
                        _state.Stall = newState.Stall;
                    }
                    if (newState.LineLeft != _state.LineLeft)
                    {
                        notify.Add("LINELEFT");
                        _state.LineLeft = newState.LineLeft;
                    }
                    if (newState.LineRight != _state.LineRight)
                    {
                        notify.Add("LINERIGHT");
                        _state.LineRight = newState.LineRight;
                    }
                    break;
                
                default:
                    LogError("Unrecognized sensor type");
                    //throw new ArgumentException("Sensor update error");
                    break;
            }

            // notify general subscribers
            subMgrPort.Post(new submgr.Submit(_state, dssp.DsspActions.ReplaceRequest));

            // notify selective subscribers
            submgr.Submit sub = new submgr.Submit(_state, dssp.DsspActions.ReplaceRequest, notify.ToArray());
            subMgrPort.Post(sub);

            yield break;
        }


        /// <summary>
        /// Get Handler
        /// </summary>
        [ServiceHandler(ServiceHandlerBehavior.Concurrent)]
        public virtual IEnumerator<ITask> GetHandler(Get get)
        {
            //_scribblerComm.SendCommand(new ScribblerCommand((byte)ScribblerHelper.Commands.GET_ALL_BINARY));
            
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
