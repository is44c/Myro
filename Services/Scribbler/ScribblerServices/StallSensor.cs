//------------------------------------------------------------------------------
// Scribbler Stall Service
//
//  Provides standard interface for the scribbler's stall sensor      
//
//------------------------------------------------------------------------------

using Microsoft.Ccr.Core;
using Microsoft.Dss.Core;
using Microsoft.Dss.Core.Attributes;
using Microsoft.Dss.ServiceModel.Dssp;
using Microsoft.Dss.ServiceModel.DsspServiceBase;
using Microsoft.Dss.Core.DsspHttp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security.Permissions;
using System.Threading;
using xml = System.Xml;
using soap = W3C.Soap;
using Myro.Services;

using brick = Myro.Services.Scribbler.ScribblerBase.Proxy;
using vector = Myro.Services.Generic.Vector;

namespace Myro.Services.Scribbler.StallSensor
{

    public static class Contract
    {
        public const string Identifier = "http://www.roboteducation.org/schemas/2008/06/scribblerstall.html";
    }

    [DisplayName("Scribbler Stall")]
    [Description("The Scribbler Stall Service")]
    [Contract(Contract.Identifier)]
    [AlternateContract(vector.Contract.Identifier)] //implementing the generic contract
    public class StallService : vector.VectorService
    {

        [Partner("ScribblerBase",
            Contract = brick.Contract.Identifier,
            CreationPolicy = PartnerCreationPolicy.UseExistingOrCreate,
            Optional = false)]
        private brick.ScribblerOperations _scribblerPort = new brick.ScribblerOperations();

        private bool _subscribed = false;

        /// <summary>
        /// Default Service Constructor
        /// </summary>
        public StallService(DsspServiceCreationPort creationPort) :
            base(creationPort)
        {
            _state = new vector.VectorState(
                new List<double> { 0.0 },
                new List<string>(),
                DateTime.Now);
        }

        /// <summary>
        /// Service Start
        /// </summary>
        protected override void Start()
        {
            base.Start();
            //LogInfo(LogGroups.Console, "Service uri: ");

            SubscribeToScribblerBase();
        }

        /// <summary>
        /// Subscribe to appropriate sensors on Scribbler base
        /// </summary>
        private void SubscribeToScribblerBase()
        {
            // Create a notification port
            brick.ScribblerOperations _notificationPort = new brick.ScribblerOperations();

            //create a custom subscription request
            brick.MySubscribeRequestType request = new brick.MySubscribeRequestType();

            //select only the sensor and port we want
            //NOTE: this name must match the scribbler sensor name.
            request.Sensors = new List<string>();

            //request.Sensors.Add("IRLeft");
            //request.Sensors.Add("IRRight");
            request.Sensors.Add("Stall");

            //Subscribe to the ScribblerBase and wait for a response
            Activate(
                Arbiter.Choice(_scribblerPort.SelectiveSubscribe(request, _notificationPort),
                    delegate(SubscribeResponseType Rsp)
                    {
                        //update our state with subscription status
                        _subscribed = true;

                        LogInfo("ScribblerStall subscription success");

                        //Subscription was successful, start listening for sensor change notifications
                        Activate(
                            Arbiter.Receive<brick.Replace>(true, _notificationPort, SensorNotificationHandler)
                        );
                    },
                    delegate(soap.Fault F)
                    {
                        LogError("ScribblerStall subscription failed");
                    }
                )
            );
        }

        /// <summary>
        /// Handle sensor update message from Scribbler
        /// </summary>
        public void SensorNotificationHandler(brick.Replace notify)
        {
            double[] values = { (notify.Body.Stall ? 1.0 : 0.0) };
            OperationsPort.Post(new vector.SetAllElements(new List<double>(values)));
        }
    }


}
