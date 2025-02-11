//-----------------------------------------------------------------------------
// FILE:        AuthTestWcfProxy.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit test WFC Authentication proxy.

//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:2.0.50727.42
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

[assembly: System.Runtime.Serialization.ContractNamespaceAttribute("http://lilltek.com/platform/2007/03/10", ClrNamespace = "lilltek.com.platform._2007._03._10")]

namespace lilltek.com.platform._2007._03._10
{
    using System.Runtime.Serialization;

    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Runtime.Serialization", "3.0.0.0")]
    [System.Runtime.Serialization.DataContractAttribute()]
    public partial class AuthenticationResult : object, System.Runtime.Serialization.IExtensibleDataObject
    {
        private System.Runtime.Serialization.ExtensionDataObject extensionDataField;

        private System.TimeSpan MaxCacheTimeField;

        private string MessageField;

        private LillTek.Datacenter.AuthenticationStatus StatusField;

        public System.Runtime.Serialization.ExtensionDataObject ExtensionData
        {
            get
            {
                return this.extensionDataField;
            }
            set
            {
                this.extensionDataField = value;
            }
        }

        [System.Runtime.Serialization.DataMemberAttribute()]
        public System.TimeSpan MaxCacheTime
        {
            get
            {
                return this.MaxCacheTimeField;
            }
            set
            {
                this.MaxCacheTimeField = value;
            }
        }

        [System.Runtime.Serialization.DataMemberAttribute()]
        public string Message
        {
            get
            {
                return this.MessageField;
            }
            set
            {
                this.MessageField = value;
            }
        }

        [System.Runtime.Serialization.DataMemberAttribute()]
        public LillTek.Datacenter.AuthenticationStatus Status
        {
            get
            {
                return this.StatusField;
            }
            set
            {
                this.StatusField = value;
            }
        }
    }
}


[System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "3.0.0.0")]
[System.ServiceModel.ServiceContractAttribute(Namespace = "http://lilltek.com/platform/2007/03/10", ConfigurationName = "IAuthServiceHandler")]
public interface IAuthServiceHandler
{
    [System.ServiceModel.OperationContractAttribute(Action = "http://lilltek.com/platform/2007/03/10/IAuthServiceHandler/Authenticate", ReplyAction = "http://lilltek.com/platform/2007/03/10/IAuthServiceHandler/AuthenticateResponse")]
    lilltek.com.platform._2007._03._10.AuthenticationResult Authenticate(string realm, string account, string password);
}

[System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "3.0.0.0")]
public interface IAuthServiceHandlerChannel : IAuthServiceHandler, System.ServiceModel.IClientChannel
{
}

[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "3.0.0.0")]
public partial class AuthServiceHandlerClient : System.ServiceModel.ClientBase<IAuthServiceHandler>, IAuthServiceHandler
{
    public AuthServiceHandlerClient()
    {
    }

    public AuthServiceHandlerClient(string endpointConfigurationName) :
        base(endpointConfigurationName)
    {
    }

    public AuthServiceHandlerClient(string endpointConfigurationName, string remoteAddress) :
        base(endpointConfigurationName, remoteAddress)
    {
    }

    public AuthServiceHandlerClient(string endpointConfigurationName, System.ServiceModel.EndpointAddress remoteAddress) :
        base(endpointConfigurationName, remoteAddress)
    {
    }

    public AuthServiceHandlerClient(System.ServiceModel.Channels.Binding binding, System.ServiceModel.EndpointAddress remoteAddress) :
        base(binding, remoteAddress)
    {
    }

    public lilltek.com.platform._2007._03._10.AuthenticationResult Authenticate(string realm, string account, string password)
    {
        return base.Channel.Authenticate(realm, account, password);
    }
}

