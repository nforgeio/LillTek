//-----------------------------------------------------------------------------
// FILE:        ITransactedResource.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the behavior of a resource that supports modification
//              using TransactionManager related classes. 

using System;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Transactions
{
    /// <summary>
    /// Describes the behavior of a resource that supports modification
    /// using <see cref="TransactionManager" /> related classes. 
    /// </summary>
    /// <remarks>
    /// <para>
    /// Classes that need transactional support will implement the 
    /// <see cref="ITransactedResource" /> interface.  This interface
    /// provides the mechanism necessary for a <see cref="TransactionManager" />
    /// to commit or rollback changes to a resource or to recover partially
    /// completed transactions after a process or system failure.
    /// </para>
    /// <para>
    /// During the course of a transaction, the <see cref="ITransactedResource" />
    /// will append <see cref="IOperation" />s to the transaction log via the
    /// the <see cref="BaseTransaction" />'s <see cref="BaseTransaction.Log" />
    /// method.  The operations added include at least enough information to
    /// undo the operation if necessary and as well as everything necessary to
    /// redo the transaction.  When a base or nested transaction is rolled back, 
    /// the transaction manager will submit the operations back to the resource to
    /// be undone in the reverse order that they were submitted:
    /// </para>
    /// <code language="cs">
    /// // Transaction manager rollback pseudo code
    /// 
    /// UpdateContext   context = new UpdateContext(...);
    /// bool            replay;
    /// 
    /// replay = resource.BeginUndo(context);
    /// 
    /// if (replay) {
    /// 
    ///     foreach (IOperation operation in GetReverseOperationList())
    ///         resource.Undo(context,operation);
    /// }
    /// 
    /// resource.EndUndo(context);
    /// </code>
    /// <para>
    /// The transaction manager calls the resource's <see cref="BeginUndo" />
    /// method to initiate the operation, giving the resource the chance to 
    /// perform any initialization and to return a boolean indicating whether
    /// or not the resource requires the transaction operations to be replayed
    /// by calling <see cref="Undo" /> once for each operation being undone.
    /// The transaction manager completes the sequence by calling
    /// <see cref="EndUndo" />.
    /// </para>
    /// <para>
    /// The transaction manager performs a similar sequence when a base
    /// transaction is committed.  In this case, <see cref="BeginRedo" />,
    /// <see cref="Redo" />, and <see cref="EndRedo" /> are called with
    /// the transaction's operations being submitted in the same order
    /// that they were appended to the transaction.
    /// </para>
    /// <para>
    /// Resources also need to be able to handle the recovery of
    /// partially completed transactions.  This situation occurs when
    /// the transaction manager restarts after a failure and discovers
    /// that there are partially completed transactions logged.  The
    /// transaction manager handles this by calling <see cref="BeginRecovery" />,
    /// performing any any necessary <see cref="BeginUndo" />...<see cref="EndUndo" />
    /// and <see cref="BeginRedo" />...<see cref="EndRedo" /> sequences and
    /// then calling <see cref="EndRecovery" />.  <see cref="BeginRecovery" />
    /// may return an application specific recovery context object that
    /// will be passed to all of the other calls.
    /// </para>
    /// <para><b><u>Operation Serialization</u></b></para>
    /// <para>
    /// <see cref="ITransactedResource" /> implementaqtions are responsible for managing most
    /// of the serialization of their <see cref="IOperation" /> instances.  This
    /// serialization is performed via the <see cref="ReadOperation" /> and
    /// <see cref="WriteOperation" /> methods.  Note that <see cref="TransactionManager" />
    /// takes care of the serialization and deserialization of <see cref="IOperation" />
    /// <see cref="IOperation.Description" /> property.
    /// </para>
    /// <para><b><u>Threading Model</u></b></para>
    /// <para>
    /// The threading model is simple.  All recovery, undo, and redo call
    /// sequences will be called on a single thread so it is not necessary
    /// for the resource to implement thread synchronization unless it
    /// has its own threads to worry about.
    /// </para>
    /// </remarks>
    public interface ITransactedResource
    {
        /// <summary>
        /// Returns a human readable resource name to be used for logging purposes.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Called when the <see cref="TransactionManager" /> initiates a 
        /// transaction recovery process.
        /// </summary>
        /// <param name="context">An <see cref="UpdateContext" /> holding information about the operation.</param>
        /// <remarks>
        /// Prudent resources will take this call as an opportunity to perform a 
        /// consistency check on its persisted state and throw an exception if the 
        /// resource is too damaged to be recovered.
        /// </remarks>
        void BeginRecovery(UpdateContext context);

        /// <summary>
        /// Called when the <see cref="ITransactedResource" /> completes a
        /// transaction recovery process.
        /// </summary>
        /// <param name="context">An <see cref="UpdateContext" /> holding information about the operation.</param>
        void EndRecovery(UpdateContext context);

        /// <summary>
        /// Called before submitting a set of <see cref="IOperation" /> to
        /// the resource to be undone.
        /// </summary>
        /// <param name="context">A <see cref="UpdateContext" /> holding information about the operation.</param>
        /// <returns>
        /// <c>true</c> if the resource requires the <see cref="TransactionManager" /> to
        /// call <see cref="Undo" /> for each operation in the transaction.
        /// </returns>
        bool BeginUndo(UpdateContext context);

        /// <summary>
        /// Called to undo an <see cref="IOperation" />.
        /// </summary>
        /// <param name="context">An <see cref="UpdateContext" /> holding information about the operation.</param>
        /// <param name="operation">The <see cref="IOperation" /> being undone.</param>
        /// <remarks>
        /// <note>
        /// It is possible that the <see cref="IOperation" /> being undone was never actually
        /// performed against the resource or that it has already been undone.  The <see cref="Undo" /> 
        /// implementation needs to be smart enough to handle these situations.
        /// </note>
        /// </remarks>
        void Undo(UpdateContext context, IOperation operation);

        /// <summary>
        /// Called to indicate that the undo operation is complete.
        /// </summary>
        /// <param name="context">A <see cref="UpdateContext" /> holding information about the operation.</param>
        void EndUndo(UpdateContext context);

        /// <summary>
        /// Called before submitting a set of <see cref="IOperation" />s to
        /// the resource to be redone.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the resource requires the <see cref="TransactionManager" /> to
        /// call <see cref="Redo" /> for each operation in the transaction.
        /// </returns>
        bool BeginRedo(UpdateContext context);

        /// <summary>
        /// Called to redo an <see cref="IOperation" />.
        /// </summary>
        /// <param name="context">An <see cref="UpdateContext" /> holding information about the operation.</param>
        /// <param name="operation">The <see cref="IOperation" /> being redone.</param>
        /// <remarks>
        /// <note>
        /// It is possible that the <see cref="IOperation" /> being redone has already
        /// been performed against the resource.  The <see cref="Redo" /> implementation
        /// needs to be smart enough to handle this situation.
        /// </note>
        /// </remarks>
        void Redo(UpdateContext context, IOperation operation);

        /// <summary>
        /// Called to indicate that the redo operation is complete.
        /// </summary>
        /// <param name="context">An <see cref="UpdateContext" /> holding information about the operation.</param>
        void EndRedo(UpdateContext context);

        /// <summary>
        /// Deserializes an <see cref="IOperation" /> from a stream.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <returns>The reconstituted <see cref="IOperation" /> instance.</returns>
        IOperation ReadOperation(EnhancedStream input);

        /// <summary>
        /// Serializes an <see cref="IOperation" /> to a stream.
        /// </summary>
        /// <param name="output">The output stream.</param>
        /// <param name="operation">The <see cref="IOperation" /> to be written.</param>
        void WriteOperation(EnhancedStream output, IOperation operation);
    }
}
