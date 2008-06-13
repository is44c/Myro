//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     DSS Runtime Version: 2.0.730.3
//     CLR Runtime Version: 2.0.50727.1434
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using Microsoft.Ccr.Core;
using Microsoft.Dss.Core;
using Microsoft.Dss.Core.Attributes;
using Microsoft.Dss.ServiceModel.Dssp;
using Microsoft.Dss.ServiceModel.DsspServiceBase;
using submgr = Microsoft.Dss.Services.SubscriptionManager;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml;
using System.Linq;
using W3C.Soap;
using Myro.Utilities;
using partnerList = Microsoft.Dss.Services.PartnerListManager;
using analog = Microsoft.Robotics.Services.AnalogSensor.Proxy;
using analogArray = Microsoft.Robotics.Services.AnalogSensorArray.Proxy;
using contact = Microsoft.Robotics.Services.ContactSensor.Proxy;

namespace Myro.Services.Generic.Vector
{
    /// <summary>
    /// This is a base class for building services that can represent their
    /// data as a Vector, or a set of key-value pairs.  This class makes
    /// building new services easy, since it takes care of all get and set
    /// requests (get/set by index, by key, or all elements at once), and also
    /// handles subscribers.
    /// 
    /// To build a sensor service, you have to implement some way of updating
    /// the state.  This can either be with a subscription to another service,
    /// a periodic timer, or with a callback (by overriding GetCallback, which
    /// will be called whenever any part of the state is requested).
    /// 
    /// To build an actuator service, your options are basically the same.  If
    /// another service subscribes to this one, you don't have to do anything.
    /// Otherwise, you can add a callback (by overriding SetCallback), or you
    /// can read the state periodically.
    /// 
    /// When overriding GetCallback() and SetCallback(), you can throw an
    /// exception to indicate an error.  If you do, subscribers will not be
    /// notified, and the Vector class will encapsulate the exception with a
    /// Fault (using RSUtils.FaultOfException()), which you can either
    /// retrieve on the other end using RSUtils.ExceptionOfFault(), or if you
    /// use RSUtils.SyncReceive<>(), this method will automatically retrieve
    /// and re-throw the exception.  NOTE:  If a Fault is generated by the DSS
    /// system, and does not encapsulate an exception,
    /// RSUtils.ExceptionOfFault() will instead throw a FaultReceivedException,
    /// which encapsulates the Fault.
    /// 
    /// You do not need to actually modify the state in SetCallback(), it will
    /// already have been modified by the Vector base class.  Likewise, you do
    /// not need to throw exceptions from the above callbacks for index
    /// out-of-bounds, invalid key, etc, these will be handled automatically.
    /// The callbacks simply allow you to communicate with the hardware or with
    /// another service.
    /// 
    /// Although the state properties are public (this is necessary for
    /// serialization), DO NOT modify them directly, because there is an
    /// internal cache of key-index mappings, which must be rebuilt if a key
    /// changes, or if the number of keys or values changes.
    /// 
    /// The key and value lists do not have to have the same lengths, if there
    /// are more values than keys, the end values will only be accessible by
    /// index, and if there are more keys than values, those keys will simply
    /// by unused (and throw UnknownKeyExceptions).  This flexibility implies
    /// that you do not even have to use the keys at all if clients will
    /// always access elements by index.
    /// 
    /// Also, auto-subscription is a way of hooking this service up to 
    /// automatically get its state from one or more generic contract services,
    /// which must be either Analog, AnalogArray, or Contact.  You set up auto-
    /// subscription using partners whose names start with "auto-".  See the
    /// developers manual for instructions on how to do this (this is how you
    /// make Myro work with pre-made generic robot services).
    /// 
    /// If all else fails, see the Myro Developer's Manual.
    /// </summary>
    [DisplayName("Vector")]
    [Description("A Generic Vector Service")]
    [Contract(Contract.Identifier)]
    public class VectorService : DsspServiceBase
    {
        #region Member variables

        /// <summary>
        /// _state
        /// </summary>
        [ServiceState()]
        protected VectorState _state = new VectorState();

        /// <summary>
        /// _main Port
        /// </summary>
        [ServicePort("/vector", AllowMultipleInstances = false)]
        protected VectorOperations _operationsPort = new VectorOperations();
        protected VectorOperations OperationsPort { get { return _operationsPort; } private set { _operationsPort = value; } }

        /// <summary>
        /// Subscription manager port
        /// </summary>
        [Partner("SubMgr",
            Contract = submgr.Contract.Identifier,
            CreationPolicy = PartnerCreationPolicy.CreateAlways,
            Optional = false)]
        private submgr.SubscriptionManagerPort _subMgrPort = new submgr.SubscriptionManagerPort();

        #endregion

        /// <summary>
        /// Default Service Constructor
        /// </summary>
        public VectorService(DsspServiceCreationPort creationPort) : base(creationPort) { }

        /// <summary>
        /// Service Start
        /// </summary>
        protected override void Start()
        {
            base.Start();
            subscribeAutos();
        }

        #region Vector service handlers
        /// <summary>
        /// Callback giving you the opportunity to set the state before it is
        /// retrieved due to a request.  The requestInfo parameter will be
        /// either a GetElementRequestInfo class, or a GetAllRequestInfo class.
        /// Use the "is" keyword to find out which one (and thus what type the
        /// request was).  This class, once casted to the right type, contains
        /// information about the specific request.  See the Vector class
        /// description for more information.
        /// </summary>
        protected virtual void GetCallback(GetRequestInfo request)
        {
        }

        /// <summary>
        /// Callback giving you the opportunity to take action after the state
        /// is modified by a request.  The requestInfo parameter will be
        /// either a SetElementRequestInfo class, or a SetAllRequestInfo class.
        /// Use the "is" keyword to find out which one (and thus what type the
        /// request was).  This class, once casted to the right type, contains
        /// information about the specific request.  See the Vector class
        /// description for more information.
        /// </summary>
        protected virtual void SetCallback(SetRequestInfo request)
        {
        }

        [ServiceHandler(ServiceHandlerBehavior.Exclusive)]
        public IEnumerator<ITask> ReplaceHandler(Replace replace)
        {
            _state = replace.Body;
            try
            {
                SetCallback(new SetAllRequestInfo()
                {
                    Timestamp = replace.Body.Timestamp,
                    Values = replace.Body.Values
                });
                replace.ResponsePort.Post(DefaultReplaceResponseType.Instance);
                SendNotification<Replace>(replace);
            }
            catch (Exception e)
            {
                replace.ResponsePort.Post(RSUtils.FaultOfException(e));
            }
            yield break;
        }

        [ServiceHandler(ServiceHandlerBehavior.Concurrent)]
        public IEnumerator<ITask> GetByIndexHandler(GetByIndex get)
        {
            try
            {
                GetElementResponseType response = new GetElementResponseType()
                {
                    Value = _state.Get(get.Body.Index),
                    Timestamp = _state.Timestamp
                };
                GetCallback(new GetElementRequestInfo()
                {
                    RequestType = RequestType.ByIndex,
                    Index = get.Body.Index,
                    Key = ((_state.Keys.Count >= (get.Body.Index + 1)) ? _state.Keys[get.Body.Index] : "")
                });
                get.ResponsePort.Post(response);
            }
            catch (Exception e)
            {
                get.ResponsePort.Post(RSUtils.FaultOfException(e));
            }
            yield break;
        }

        [ServiceHandler(ServiceHandlerBehavior.Concurrent)]
        public IEnumerator<ITask> GetByKeyHandler(GetByKey get)
        {
            try
            {
                GetElementResponseType response = new GetElementResponseType()
                {
                    Value = _state.Get(get.Body.Key),
                    Timestamp = _state.Timestamp
                };
                GetCallback(new GetElementRequestInfo()
                {
                    RequestType = RequestType.ByKey,
                    Index = _state.indexCache[get.Body.Key],
                    Key = get.Body.Key
                });
                get.ResponsePort.Post(response);
            }
            catch (Exception e)
            {
                get.ResponsePort.Post(RSUtils.FaultOfException(e));
            }
            yield break;
        }

        [ServiceHandler(ServiceHandlerBehavior.Concurrent)]
        public IEnumerator<ITask> GetAllHandler(GetAllElements get)
        {
            // No exception check here - none can be thrown
            GetCallback(new GetAllRequestInfo() { });
            get.ResponsePort.Post(new GetAllResponseType()
                {
                    Values = _state.Values,
                    Timestamp = _state.Timestamp
                });
            yield break;
        }

        [ServiceHandler(ServiceHandlerBehavior.Exclusive)]
        public IEnumerator<ITask> SetByIndexHandler(SetByIndex set)
        {
            try
            {
                _state.Set(set.Body.Index, set.Body.Value, set.Body.Timestamp);
                SetCallback(new SetElementRequestInfo()
                {
                    RequestType = RequestType.ByIndex,
                    Index = set.Body.Index,
                    Key = ((_state.Keys.Count >= (set.Body.Index + 1)) ? _state.Keys[set.Body.Index] : ""),
                    Timestamp = set.Body.Timestamp,
                    Value = set.Body.Value
                });
                set.ResponsePort.Post(DefaultUpdateResponseType.Instance);
                SendNotification<SetByIndex>(set);
            }
            catch (Exception e)
            {
                set.ResponsePort.Post(RSUtils.FaultOfException(e));
            }
            yield break;
        }

        [ServiceHandler(ServiceHandlerBehavior.Exclusive)]
        public IEnumerator<ITask> SetByKeyHandler(SetByKey set)
        {
            try
            {
                _state.Set(set.Body.Key, set.Body.Value, set.Body.Timestamp);
                SetCallback(new SetElementRequestInfo()
                {
                    RequestType = RequestType.ByKey,
                    Index = _state.indexCache[set.Body.Key],
                    Key = set.Body.Key,
                    Timestamp = set.Body.Timestamp,
                    Value = set.Body.Value
                });
                set.ResponsePort.Post(DefaultUpdateResponseType.Instance);
                SendNotification<SetByKey>(set);
            }
            catch (Exception e)
            {
                set.ResponsePort.Post(RSUtils.FaultOfException(e));
            }
            yield break;
        }

        [ServiceHandler(ServiceHandlerBehavior.Exclusive)]
        public IEnumerator<ITask> SetAllHandler(SetAllElements setAll)
        {
            _state.Values = setAll.Body.Values;
            _state.Timestamp = setAll.Body.Timestamp;
            SetCallback(new SetAllRequestInfo() { Values = setAll.Body.Values, Timestamp = setAll.Body.Timestamp });
            setAll.ResponsePort.Post(DefaultUpdateResponseType.Instance);
            SendNotification<SetAllElements>(setAll);
            yield break;
        }

        [ServiceHandler(ServiceHandlerBehavior.Exclusive)]
        public IEnumerator<ITask> SubscribeHandler(Subscribe subscribe)
        {
            yield return Arbiter.Choice(
                SubscribeHelper(_subMgrPort, subscribe.Body, subscribe.ResponsePort),
                delegate(SuccessResult success)
                {
                    base.SendNotification<Replace>(_subMgrPort, subscribe.Body.Subscriber, _state);
                },
                delegate(Exception error)
                {
                    base.LogError("Error adding subscriber " + subscribe.Body.Subscriber, error);
                });
        }

        #endregion

        #region Private and protected methods
        protected void SendNotification<T>(T message) where T : DsspOperation
        {
            base.SendNotification<T>(_subMgrPort, message);
        }
        #endregion

        #region Auto subscription methods
        /// <summary>
        /// Subscribes to any services specified in the partner list to
        /// automatically update the state.
        /// </summary>
        private void subscribeAutos()
        {
            // Get the list of partners whose names start with "auto:"
            var request = new partnerList.GetOperation()
            {
                Body = new GetRequestType(),
                ResponsePort = new DsspResponsePort<PartnerListType>()
            };
            base.PartnerListManagerPort.Post(request);
            Activate(Arbiter.Choice<PartnerListType, Fault>(
                request.ResponsePort,
                delegate(PartnerListType partners)
                {
                    string autoPrefix = "auto-";
                    List<PartnerType> autoPartners = new List<PartnerType>(
                        from partner in partners.PartnerList
                        where partner.Name.Name.StartsWith(autoPrefix)
                        select partner);

                    // This method will take care of subscribing and updating state
                    if (autoPartners.Count > 0)
                        subscribeAutos2(autoPartners, autoPrefix.Length);
                },
                delegate(Fault failure)
                {
                    LogError("Fault while getting partner list to subscribe to autos", failure);
                }));
        }

        /// <summary>
        /// Once we have a list of auto partners, this method creates AutoDefiniton
        /// objects for each auto partner, and calls subscribeAutoSingle to subscribe
        /// to each one.
        /// </summary>
        /// <param name="partList"></param>
        /// <param name="removeFromName"></param>
        private void subscribeAutos2(IList<PartnerType> partList, int removeFromName)
        {
            // Create AutoDefinition objects
            List<AutoDefinition> autoDefs = new List<AutoDefinition>(partList.Count);
            int lastIndex = 0;
            List<string> allKeys = new List<string>();
            foreach (var part in partList)
            {
                // Create AutoDefinition object and add to list
                string[] keys = part.Name.Name.Substring(0, removeFromName).Split(',');
                AutoDefinition autoDef = new AutoDefinition()
                {
                    infoAsPartner = part,
                    startIndex = lastIndex,
                    count = keys.Length,
                    keys = keys
                };
                autoDefs.Add(autoDef);

                // Update loop variables
                lastIndex += keys.Length;
                allKeys.AddRange(keys);
            }

            // Create the new vector state reflecting the keys
            List<double> allValues = new List<double>(from k in allKeys select 0.0);
            VectorState newState = new VectorState(allValues, allKeys, DateTime.Now);
            var responsePort = new DsspResponsePort<DefaultReplaceResponseType>();
            OperationsPort.Post(new Replace(newState, responsePort));
            Activate(Arbiter.Choice(responsePort,
                delegate(DefaultReplaceResponseType r) { },
                delegate(Fault f) { LogError("Fault while replacing initial vector state", f); }));

            // Try to subscribe to compatible contracts with the AutoDefinition objects
            foreach (var autoDef in autoDefs)
            {
                Activate(Arbiter.Choice(
                    RSUtils.FindCompatibleContract(TaskQueue, new Uri(autoDef.infoAsPartner.Service), new List<string>() { 
                        analog.Contract.Identifier, analogArray.Contract.Identifier, contact.Contract.Identifier }),
                    delegate(ServiceInfoType serviceInfoResponse)
                    {
                        try
                        {
                            subscribeAutoSingle(autoDef, serviceInfoResponse);
                            autoDef.infoAsConnected = serviceInfoResponse;
                        }
                        catch (Exception e)
                        {
                            LogError("Exception while subscribing to auto partner", e);
                        }
                    },
                    delegate(Fault failure)
                    {
                        Exception e = RSUtils.ExceptionOfFault(failure);
                        if (e is NoContractFoundException)
                        {
                            LogError("Could not subscribe to auto partner " + autoDef.infoAsPartner.Name + ".  Could not find a supported contract.");
                        }
                        else if (e is Exception)
                        {
                            LogError("Fault while searching for compatible contract", failure);
                        }
                    }));
            }
        }

        /// <summary>
        /// Tries to subscribe to an auto partner single contract, or throws an exception
        /// if we don't support the contract.
        /// </summary>
        /// <param name="def"></param>
        /// <param name="serviceInfo"></param>
        private void subscribeAutoSingle(AutoDefinition def, ServiceInfoType serviceInfo)
        {
            Console.WriteLine("Trying to subscribe " + def.infoAsPartner.Name + " to " + serviceInfo.Service + " with contract " + serviceInfo.Contract);
            // Check for each contract we know about and subscribe.
            if (serviceInfo.Contract.Equals(analog.Contract.Identifier))
            {
                var partnerPort = ServiceForwarder<analog.AnalogSensorOperations>(new Uri(serviceInfo.Service));
                var notifyPort = new Port<analog.Replace>();
                try
                {
                    RSUtils.RecieveSync(TaskQueue, partnerPort.Subscribe(notifyPort, typeof(analog.Replace)));
                    Activate(Arbiter.Receive(true, notifyPort,
                        delegate(analog.Replace replace)
                        {
                            OperationsPort.Post(new SetByIndex(new SetByIndexRequestType()
                            {
                                Index = def.startIndex,
                                Value = replace.Body.RawMeasurement,
                                Timestamp = replace.Body.TimeStamp
                            }));
                        }));
                }
                catch (FaultReceivedException)
                {
                    // If the subscribe failed, throw this.
                    throw new UnrecognizedContractException();
                }
            }
            else if (serviceInfo.Contract.Equals(analogArray.Contract.Identifier))
            {
                var partnerPort = ServiceForwarder<analogArray.AnalogSensorOperations>(new Uri(serviceInfo.Service));
                var notifyPort = new Port<analogArray.Replace>();
                try
                {
                    RSUtils.RecieveSync(TaskQueue, partnerPort.Subscribe(notifyPort, typeof(analogArray.Replace)));
                    Activate(Arbiter.Receive(true, notifyPort,
                        delegate(analogArray.Replace replace)
                        {
                            int maxCount = replace.Body.Sensors.Count > def.count ? def.count : replace.Body.Sensors.Count;
                            for (int i = 0; i < maxCount; i++)
                                OperationsPort.Post(new SetByIndex(new SetByIndexRequestType()
                                {
                                    Index = def.startIndex + i,
                                    Value = replace.Body.Sensors[i].RawMeasurement,
                                    Timestamp = replace.Body.Sensors[i].TimeStamp
                                }));
                        }));
                }
                catch (FaultReceivedException)
                {
                    // If the subscribe failed, throw this.
                    throw new UnrecognizedContractException();
                }
            }
            else if (serviceInfo.Contract.Equals(contact.Contract.Identifier))
            {
                var partnerPort = ServiceForwarder<contact.ContactSensorArrayOperations>(new Uri(serviceInfo.Service));
                var notifyPort = new PortSet<contact.Replace, contact.Update>();
                RSUtils.RecieveSync(TaskQueue, partnerPort.Subscribe(notifyPort, typeof(contact.Replace), typeof(contact.Update)));
                Activate<Receiver>(
                    Arbiter.Receive<contact.Replace>(true, notifyPort,
                    delegate(contact.Replace replace)
                    {
                        int maxCount = replace.Body.Sensors.Count > def.count ? def.count : replace.Body.Sensors.Count;
                        for (int i = 0; i < maxCount; i++)
                            OperationsPort.Post(new SetByIndex(new SetByIndexRequestType()
                            {
                                Index = def.startIndex + i,
                                Value = replace.Body.Sensors[i].Pressed ? 1.0 : 0.0,
                                Timestamp = replace.Body.Sensors[i].TimeStamp
                            }));
                    }),
                    Arbiter.Receive<contact.Update>(true, notifyPort,
                    delegate(contact.Update update)
                    {
                        LogError("Vector got update and not updating!!!");
                    }));
            }
            else
                throw new UnrecognizedContractException();
        }

        class UnrecognizedContractException : Exception { }
        class AutoDefinition
        {
            public PartnerType infoAsPartner;
            public int startIndex;
            public int count;
            public string[] keys;

            public ServiceInfoType infoAsConnected = null;
        }
        #endregion

    }

    #region Callback request info types
    /// <summary>
    /// For the RequestType member of GetElementRequestInfo and
    /// SetElementRequestInfo, this indicates whether the service request was
    /// (Get/Set)ByIndex, or (Get/Set)ByKey.
    /// </summary>
    public enum RequestType { ByIndex, ByKey }

    /// <summary>
    /// The abstract base class for GetElementRequestInfo and GetAllRequestInfo
    /// </summary>
    public abstract class GetRequestInfo
    {
    }

    /// <summary>
    /// Passed into GetCallback, provides information about the actual service
    /// request, if the request was either GetByIndex and GetByKey.  The
    /// RequestType property indicates whether the request was GetByIndex or
    /// GetByKey.
    /// </summary>
    public class GetElementRequestInfo : GetRequestInfo
    {
        public RequestType RequestType { get; set; }
        public int Index { get; set; }
        public string Key { get; set; }
    }

    /// <summary>
    /// Passed into GetCallback, if the request was GetState.  This does not
    /// contain any members, but only indicates that the service request was
    /// GetState.
    /// </summary>
    public class GetAllRequestInfo : GetRequestInfo
    {
    }

    /// <summary>
    /// The abstract base class for SetElementRequestInfo and SetAllRequestInfo
    /// </summary>
    public abstract class SetRequestInfo
    {
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Passed into SetCallback, provides information about the actual service
    /// request, if the request was either SetByIndex and SetByKey.  The
    /// RequestType property indicates whether the request was SetByIndex or
    /// SetByKey.
    /// </summary>
    public class SetElementRequestInfo : SetRequestInfo
    {
        public RequestType RequestType { get; set; }
        public int Index { get; set; }
        public string Key { get; set; }
        public double Value { get; set; }
    }

    /// <summary>
    /// Passed into SetCallback, if the request was SetAll.  This contains the
    /// values that were set in the state.
    /// </summary>
    public class SetAllRequestInfo : SetRequestInfo
    {
        public List<double> Values { get; set; }
    }
    #endregion


}
