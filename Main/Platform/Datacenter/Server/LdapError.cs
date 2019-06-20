//-----------------------------------------------------------------------------
// FILE:        LdapError.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the LDAP error codes.

using System;

namespace LillTek.Datacenter.Server
{
    /// <summary>
    /// Defines the LDAP error codes.
    /// </summary>
    internal enum LdapError
    {
        /// <summary>
        /// Indicates the requested client operation completed successfully.
        /// </summary>
        SUCCESS = 0,

        /// <summary>
        /// Indicates an internal error. The server is unable to respond with a more 
        /// specific error and is also unable to properly respond to a request. It does 
        /// not indicate that the client has sent an erroneous message.
        /// </summary>
        OPERATIONS_ERROR = 1,

        /// <summary>
        /// Indicates that the server has received an invalid or malformed request from the client.
        /// </summary>
        PROTOCOL_ERROR = 2,

        /// <summary>
        /// Indicates that the operation's time limit specified by either the client or the 
        /// server has been exceeded. On search operations, incomplete results are returned.
        /// </summary>
        TIMELIMIT_EXCEEDED = 3,

        /// <summary>
        /// Indicates that in a search operation, the size limit specified by the client or 
        /// the server has been exceeded. Incomplete results are returned.
        /// </summary>
        SIZELIMIT_EXCEEDED = 4,

        /// <summary>
        /// Does not indicate an error condition. Indicates that the results of a compare operation are false.
        /// </summary>
        COMPARE_FALSE = 5,

        /// <summary>
        /// Does not indicate an error condition. Indicates that the results of a compare operation are true.
        /// </summary>
        COMPARE_TRUE = 6,

        /// <summary>
        /// Indicates that during a bind operation the client requested an authentication method not 
        /// supported by the LDAP server.
        /// </summary>
        AUTH_METHOD_NOT_SUPPORTED = 7,

        /// <summary>
        /// <para>
        /// Indicates one of the following:
        /// </para>
        /// <list type="bullet">
        ///     <item>
        ///     In bind requests, the LDAP server accepts only strong authentication.
        ///     </item>
        ///     <item>
        ///     In a client request, the client requested an operation such as delete that requires strong authentication.
        ///     </item>
        ///     <item>
        ///     In an unsolicited notice of disconnection, the LDAP server discovers the security protecting the communication between 
        /// the client and server has unexpectedly failed or been compromised.
        ///     </item>
        /// </list>
        /// </summary>
        STRONG_AUTH_REQUIRED = 8,

        /// <summary>
        /// Does not indicate an error condition. In LDAPv3, indicates that the server does not hold the target 
        /// entry of the request, but that the servers in the referral field may.
        /// </summary>
        REFERRAL = 10,

        /// <summary>
        /// Indicates that an LDAP server limit set by an administrative authority has been exceeded.
        /// </summary>
        ADMINLIMIT_EXCEEDED = 11,

        /// <summary>
        /// Indicates that the LDAP server was unable to satisfy a request because one or more critical 
        /// extensions were not available. Either the server does not support the control or the control is not appropriate for the operation type.
        /// </summary>
        UNAVAILABLE_CRITICAL_EXTENSION = 12,

        /// <summary>
        /// Indicates that the session is not protected by a protocol such as Transport Layer Security (TLS), which provides session confidentiality.
        /// </summary>
        CONFIDENTIALITY_REQUIRED = 13,

        /// <summary>
        /// Does not indicate an error condition, but indicates that the server is ready for the next step in the process. The client must send 
        /// the server the same SASL mechanism to continue the process. 
        /// </summary>
        SASL_BIND_IN_PROGRESS = 14,

        /// <summary>
        /// Indicates that the attribute specified in the modify or compare operation does not exist in the entry.
        /// </summary>
        NO_SUCH_ATTRIBUTE = 16,

        /// <summary>
        /// Indicates that the attribute specified in the modify or add operation does not exist in the LDAP server's schema.
        /// </summary>
        UNDEFINED_TYPE = 17,

        /// <summary>
        /// Indicates that the matching rule specified in the search filter does not match a rule defined for the attribute's syntax.
        /// </summary>
        INAPPROPRIATE_MATCHING = 18,

        /// <summary>
        /// Indicates that the attribute value specified in a modify, add, or modify DN operation violates constraints placed on 
        /// the attribute. The constraint can be one of size or content (string only, no binary).
        /// </summary>
        CONSTRAINT_VIOLATION = 19,

        /// <summary>
        /// Indicates that the attribute value specified in a modify or add operation already exists as a value for that attribute.
        /// </summary>
        TYPE_OR_VALUE_EXISTS = 20,

        /// <summary>
        /// Indicates that the attribute value specified in an add, compare, or modify operation is an unrecognized or 
        /// invalid syntax for the attribute.
        /// </summary>
        INVALID_SYNTAX = 21,

        /// <summary>
        /// <para>
        /// Indicates the target object cannot be found. This code is not returned on following operations:
        /// </para>
        /// <list type="bullet">
        ///     <item>
        ///     Search operations that find the search base but cannot find any entries that match the search filter.
        ///     </item>
        ///     <item>
        ///     Bind operations.
        ///     </item>
        /// </list>
        /// </summary>
        NO_SUCH_OBJECT = 32,

        /// <summary>
        /// Indicates that an error occurred when an alias was dereferenced.
        /// </summary>
        ALIAS_PROBLEM = 33,

        /// <summary>
        /// Indicates that the syntax of the DN is incorrect. (If the DN syntax is correct, but the LDAP server's structure 
        /// rules do not permit the operation, the server returns UNWILLING_TO_PERFORM.)
        /// </summary>
        INVALID_DN_SYNTAX = 34,

        /// <summary>
        /// Indicates that the specified operation cannot be performed on a leaf entry. (This code is not currently 
        /// in the LDAP specifications, but is reserved for this constant.)
        /// </summary>
        IS_LEAF = 35,

        /// <summary>
        /// Indicates that during a search operation, either the client does not have access rights to read the 
        /// aliased object's name or dereferencing is not allowed.
        /// </summary>
        ALIAS_DEREF_PROBLEM = 36,

        /// <summary>
        /// <para>
        /// Indicates that during a bind operation, the client is attempting to use an authentication method that 
        /// the client cannot use correctly. For example, either of the following cause this error:
        /// </para>
        /// <list type="bullet">
        ///     <item>
        ///     The client returns simple credentials when strong credentials are required. 
        ///     </item>
        ///     <item>
        ///     The client returns a DN and a password for a simple bind when the entry does not have a password defined.
        ///     </item>
        /// </list>
        /// </summary>
        INAPPROPRIATE_AUTH = 48,

        /// <summary>
        /// <para>
        /// Indicates that during a bind operation one of the following occurred:
        /// </para>
        /// <list type="bullet">
        ///     <item>
        ///     The client passed either an incorrect DN or password.
        ///     </item>
        ///     <item>
        ///     The password is incorrect because it has expired, intruder detection 
        ///     has locked the account, or some other similar reason.
        ///     </item>
        /// </list>
        /// </summary>
        INVALID_CREDENTIALS = 49,

        /// <summary>
        /// Indicates that the caller does not have sufficient rights to perform the requested operation.
        /// </summary>
        INSUFFICIENT_ACCESS = 50,

        /// <summary>
        /// Indicates that the LDAP server is too busy to process the client request at this time but 
        /// if the client waits and resubmits the request, the server may be able to process it then.
        /// </summary>
        BUSY = 51,

        /// <summary>
        /// Indicates that the LDAP server cannot process the client's bind request, usually because it is shutting down.
        /// </summary>
        UNAVAILABLE = 52,

        /// <summary>
        /// <para>
        /// Indicates that the LDAP server cannot process the request because of server-defined restrictions. 
        /// This error is returned for the following reasons:
        /// </para>
        /// <list type="bullet">
        ///     <item>
        ///     The add entry request violates the server's structure rules.
        ///     </item>
        ///     <item>
        ///     The modify attribute request specifies attributes that users cannot modify.
        ///     </item>
        ///     <item>
        ///     Password restrictions prevent the action.
        ///     </item>
        ///     Connection restrictions prevent the action.
        ///     <item>
        /// </item>
        /// </list>
        /// </summary>
        UNWILLING_TO_PERFORM = 53,

        /// <summary>
        /// Indicates that the client discovered an alias or referral loop, and is thus unable to complete this request.
        /// </summary>
        LOOP_DETECT = 54,

        /// <summary>
        /// <para>
        /// Indicates that the add or modify DN operation violates the schema's structure rules. For example:
        /// </para>
        /// <list type="bullet">
        ///     <item>
        ///     The request places the entry subordinate to an alias.
        ///     </item>
        ///     <item>
        ///     The request places the entry subordinate to a container that is forbidden by the containment rules.
        ///     </item>
        ///     <item>
        ///     The RDN for the entry uses a forbidden attribute type.
        ///     </item>
        /// </list>
        /// </summary>
        NAMING_VIOLATION = 64,

        /// <summary>
        /// <para>
        /// Indicates that the add, modify, or modify DN operation violates the object class rules for the entry. 
        /// For example, the following types of request return this error:
        /// </para>
        /// <list type="bullet">
        ///     <item>
        ///     The add or modify operation tries to add an entry without a value for a required attribute.
        ///     </item>
        ///     <item>
        ///     The add or modify operation tries to add an entry with a value for an attribute which the class definition does not contain.
        ///     </item>
        ///     <item>
        ///     The modify operation tries to remove a required attribute without removing the auxiliary class that defines the attribute as required.
        ///     </item>
        /// </list>
        /// </summary>
        OBJECT_CLASS_VIOLATION = 65,

        /// <summary>
        /// <para>
        /// Indicates that the requested operation is permitted only on leaf entries. 
        /// For example, the following types of requests return this error:
        /// </para>
        /// <list type="bullet">
        ///     <item>
        ///     The client requests a delete operation on a parent entry.
        ///     </item>
        ///     <item>
        ///     </item>
        ///     The client request a modify DN operation on a parent entry.
        /// </list>
        /// </summary>
        NOT_ALLOWED_ON_NONLEAF = 66,

        /// <summary>
        /// Indicates that the modify operation attempted to remove an attribute 
        /// value that forms the entry's relative distinguished name.
        /// </summary>
        NOT_ALLOWED_ON_RDN = 67,

        /// <summary>
        /// Indicates that the add operation attempted to add an entry that already exists, or that the 
        /// modify operation attempted to rename an entry to the name of an entry that already exists.
        /// </summary>
        ALREADY_EXISTS = 68,

        /// <summary>
        /// Indicates that the modify operation attempted to modify the structure rules of an object class.
        /// </summary>
        NO_OBJECT_CLASS_MODS = 69,

        /// <summary>
        /// Reserved for CLDAP. 
        /// </summary>
        RESULTS_TOO_LARGE = 70,

        /// <summary>
        /// Indicates that the modify DN operation moves the entry from one LDAP server to another 
        /// and thus requires more than one LDAP server.
        /// </summary>
        AFFECTS_MULTIPLE_DSAS = 71,

        /// <summary>
        /// Indicates an unknown error condition. This is the default value for NDS error codes 
        /// which do not map to other LDAP error codes. 
        /// </summary>
        OTHER = 80
    }
}