//------------------------------------------------------------------------------
// Scribbler Backup Monitor Service
//
//     This code was generated by the DssNewService tool.
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
using soap = W3C.Soap;
using System.Runtime.Serialization;
using submgr = Microsoft.Dss.Services.SubscriptionManager;
using brick = IPRE.ScribblerBase.Proxy;

namespace IPRE.ScribblerBackupMonitor
{

    [DisplayName("Scribbler Backup Monitor")]
    [Description("The Scribbler Backup Monitor Service")]
    [Contract(Contract.Identifier)]
    [PermissionSet(SecurityAction.PermitOnly, Name="Execution")]
    public class ScribblerBackupMonitor : DsspServiceBase
    {

        private ScribblerBackupMonitorState _state = null;

        [ServicePort("/ScribblerBackupMonitor", AllowMultipleInstances = false)]
        private ScribblerBackupMonitorOperations _mainPort = new ScribblerBackupMonitorOperations();

        [Partner("/ScribblerBase", Contract = brick.Contract.Identifier, CreationPolicy = PartnerCreationPolicy.UseExistingOrCreate, Optional = false)]
        private brick.ScribblerOperations _scribblerPort = new brick.ScribblerOperations();

        [Partner("SubMgr", Contract = submgr.Contract.Identifier, CreationPolicy = PartnerCreationPolicy.CreateAlways, Optional = false)]
        private submgr.SubscriptionManagerPort _subMgrPort = new submgr.SubscriptionManagerPort();

        private bool _subscribed = false;
        private bool beeping;

        /// <summary>
        /// Default Service Constructor
        /// </summary>
        public ScribblerBackupMonitor(DsspServiceCreationPort creationPort)
            : 
                base(creationPort)
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
                _state = new ScribblerBackupMonitorState();

                _state.PauseDuration = 800; //ms
                _state.PlayDuration = 800; //ms
                _state.Frequency1 =  1000; //Hz
                _state.Frequency2 = 0;
            }
            // Listen on the main port for requests and call the appropriate handler.
            ActivateDsspOperationHandlers();

            // Publish the service to the local Node Directory
            DirectoryInsert();

			// display HTTP service Uri
			LogInfo(LogGroups.Console, "Service uri: ");

            SubscribeToScribblerBase();
        }


        /// <summary>
        /// Subscribe to motors on Scribbler base
        /// </summary>
        private void SubscribeToScribblerBase()
        {
            // Create a notification port
            brick.ScribblerOperations _notificationPort = new brick.ScribblerOperations();

            //create a custom subscription request
            brick.MySubscribeRequestType request = new brick.MySubscribeRequestType();

            request.Sensors = new List<string>();
            request.Sensors.Add("MOTORS");
            
            //Subscribe to the ScribblerBase and wait for a response
            Activate(
                Arbiter.Choice(_scribblerPort.SelectiveSubscribe(request, _notificationPort),
                    delegate(SubscribeResponseType Rsp)
                    {
                        //update our state with subscription status
                        _subscribed = true;

                        LogInfo("Backup Monitor subscription success");

                        //Subscription was successful, start listening for sensor change notifications
                        Activate(
                            Arbiter.Receive<brick.Replace>(true, _notificationPort, MotorNotificationHandler)
                        );
                    },
                    delegate(soap.Fault F)
                    {
                        LogError("Backup Monitor subscription failed");
                    }
                )
            );
        }

        /// <summary>
        /// Handle motor update message from Scribbler
        /// </summary>
        public void MotorNotificationHandler(brick.Replace notify)
        {
            if (notify == null)
                throw new ArgumentNullException("notify");

            if (beeping)
                return;

            if (notify.Body.MotorLeft < 100 && notify.Body.MotorRight < 100)
            {
                SpawnIterator(ToneHandler);
            }
        }

        IEnumerator<ITask> ToneHandler()
        {
            beeping = true;

            brick.PlayToneMessage tone = new IPRE.ScribblerBase.Proxy.PlayToneMessage();
            tone.Duration = _state.PlayDuration;
            tone.Frequency1 = _state.Frequency1;
            tone.Frequency2 = _state.Frequency2;
            _scribblerPort.PlayTone(tone);

            //wait play time + pause time
            yield return Arbiter.Receive(false, TimeoutPort(_state.PlayDuration + _state.PauseDuration),
                delegate(DateTime t) { });

            beeping = false;

            //get state and call handler again because we still might be backing up
            yield return Arbiter.Receive<brick.ScribblerState>(false, _scribblerPort.Get(new GetRequestType()),
                delegate(brick.ScribblerState scribblerState)
                {
                    MotorNotificationHandler(new brick.Replace(scribblerState));
                }
            );

        }

        /// <summary>
        /// Get Handler
        /// </summary>
        /// <param name="get"></param>
        /// <returns></returns>
        [ServiceHandler(ServiceHandlerBehavior.Concurrent)]
        public virtual IEnumerator<ITask> GetHandler(Get get)
        {
            get.ResponsePort.Post(_state);
            yield break;
        }


        /// <summary>
        /// Replace Handler
        /// </summary>
        /// <param name="replace"></param>
        /// <returns></returns>
        [ServiceHandler(ServiceHandlerBehavior.Exclusive)]
        public virtual IEnumerator<ITask> ReplaceHandler(Replace replace)
        {
            _state = replace.Body;
            replace.ResponsePort.Post(DefaultReplaceResponseType.Instance);
            yield break;
        }
    }


}
