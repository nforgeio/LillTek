//-----------------------------------------------------------------------------
// FILE:        CompressResponseComponent.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements an OWIN middleware component that will compress the
//              response using DEFLATE or GZIP compression

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Owin;
using Microsoft.Owin.Hosting;

using Owin;

using LillTek.Common;

namespace LillTek.Owin
{
    /// <summary>
    /// Implements an OWIN middleware component that will compress the response
    /// using DEFLATE or GZIP compression if the client request has an <b>Accept-Encoding</b>
    /// header indicating that this is acceptable.
    /// </summary>
    /// <remarks>
    /// <note>
    /// This middleware component should be the first or very close to being the first
    /// component in the pipeline.
    /// </note>
    /// <note>
    /// This implementation will hold all of the compressed response data in memory via
    /// a <see cref="BlockStream"/> until control has returned from the components further 
    /// down in the pipeline.  Use of the <see cref="BlockStream"/> will avoid any large
    /// object heap issues, but applications should take the memory consumption into
    /// consideration when using this component.
    /// </note>
    /// </remarks>
    public class CompressResponseComponent
    {
        private ComponentFunc     nextComponent;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="nextComponent">The next component in the pipeline (or <c>null</c>).</param>
        public CompressResponseComponent(ComponentFunc nextComponent)
        {
            this.nextComponent = nextComponent;
        }

        /// <summary>
        /// Implements the component.
        /// </summary>
        /// <param name="environment">The OWIN environment.</param>
        /// <returns>The task used to complete the operation.</returns>
        public async Task Invoke(IDictionary<string, object> environment)
        {
            const int BlockSize = 8192;

            var context      = new OwinContext(environment);
            var request      = context.Request;
            var response     = context.Response;
            var acceptValues = request.Headers.GetValues("Accept-Encoding");

            Stream          originalBody   = null;
            Stream          compressStream = null;
            BlockStream     blockStream    = null;
            Exception       exception      = null;

            // If the client indicates that it accepts DEFLATE or GZIP compressed responses,
            // then we'll save the original response body stream and replace it with the 
            // appropriate compression stream that compresses any response data written
            // by the remaining pipeline components to a [BlockStream].  After control
            // returns, we'll write the compressed data in the [BlockStream] to the original
            // response body.
            //
            // Note that although all of the compressed response data is held in memory
            // until control is returned, using [BlockStream] rather than [MemoryStream]
            // will avoid large object heap issues.
            //
            // NOTE: We're going to favor DEFLATE over GZIP if the client supports it because
            //       the output is slightly smaller.

            if (acceptValues != null)
            {
                if (acceptValues.SingleOrDefault(v => string.Compare(v, "deflate", true) == 0) != null)
                {
                    originalBody   = response.Body;
                    blockStream    = new BlockStream(BlockSize, BlockSize);
                    compressStream =
                    response.Body  = new DeflateStream(blockStream, CompressionMode.Compress);

                    response.Headers["Content-Encoding"] = "deflate";
                }
                else if (acceptValues.SingleOrDefault(v => string.Compare(v, "gzip", true) == 0) != null)
                {
                    originalBody   = response.Body;
                    blockStream    = new BlockStream(BlockSize, BlockSize);
                    compressStream =
                    response.Body  = new GZipStream(blockStream, CompressionMode.Compress);

                    response.Headers["Content-Encoding"] = "gzip";
                }
            }

            try
            {
                if (nextComponent != null)
                {
                    await nextComponent(environment);
                }
            }
            catch (Exception e)
            {
                exception = e;
            }

            // $todo(jeff.lill): 
            //
            // When I upgrade to C# 6.0, move this code into a finally clause.  I can't do
            // this now because C# 5.0 doesn't support await in finally clauses and I don't
            // want to do this synchronously.

            if (originalBody != null)
            {
                compressStream.Flush();
                compressStream.Dispose();

                response.Body                      = originalBody;
                response.Headers["Content-Length"] = blockStream.Length.ToString();

                var cbRemaining = blockStream.Length;
                var blockIndex  = 0;

                blockStream.Position = 0;

                while (cbRemaining > 0)
                {
                    var cb = (int)Math.Min(cbRemaining, BlockSize);

                    await response.Body.WriteAsync(blockStream.RawBlockArray.GetBlock(blockIndex).Buffer, 0, cb, CancellationToken.None);

                    blockIndex++;
                    cbRemaining -= cb;
                }
                    
                blockStream.Dispose();
            }

            if (exception != null)
            {
                throw exception;
            }
        }
    }
}
