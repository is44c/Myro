//------------------------------------------------------------------------------
// Scribbler Heart Beat Service
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
using W3C.Soap;


using brick = IPRE.ScribblerBase.Proxy;

//[assembly: ContractNamespace(IPRE.ScribblerHeartBeat.Contract.Identifier, ClrNamespace = "IPRE.ScribblerHeartBeat")]
namespace IPRE.ScribblerHeartBeat
{

    public static class Contract
    {
        public const string Identifier = "http://www.roboteducation.org/scribblerheartbeat.html";
    }


    /// <summary>
    /// Main state of service
    /// </summary>
    [DataContract]
    public class ScribblerHeartBeatState
    {
        [DataMember]
        public bool Connected;

        [DataMember]
        public int whichLED;

        [DataMember]
        public bool LEDState;

        [DataMember]
        public int PauseTime;
    }

    /// <summary>
    /// Main operations port
    /// </summary>
    public class ScribblerHeartBeatOperations : PortSet<
        DsspDefaultLookup,
        DsspDefaultDrop,
        Get,
        Replace,
        Subscribe>
    {
    }

    /// <summary>
    /// http get
    /// returns entire state
    /// </summary>
    public class Get : Get<GetRequestType, PortSet<ScribblerHeartBeatState, Fault>> { }

    /// <summary>
    /// replaces entire state
    /// </summary>
    public class Replace : Replace<ScribblerHeartBeatState, PortSet<DefaultReplaceResponseType, Fault>> { }

    /// <summary>
    /// general subscription
    /// notifications on any state change
    /// </summary>
    public class Subscribe : Subscribe<SubscribeRequestType, PortSet<SubscribeResponseType, Fault>> { }

}
