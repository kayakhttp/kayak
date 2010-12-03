using System;

namespace Owin
{
    /// <summary>
    /// An HTTP application.
    /// </summary>
    public interface IApplication
    {
        /// <summary>
        /// Begins the asynchronous process to get the <see cref="IResponse"/>.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="callback">The callback.</param>
        /// <param name="state">The state.</param>
        /// <returns>The <see cref="IAsyncResult"/> that represents the asynchronous invocation.</returns>
        IAsyncResult BeginInvoke(IRequest request, AsyncCallback callback, object state);

        /// <summary>
        /// Ends the asynchronous process to get the <see cref="IResponse"/>.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <returns>The <see cref="IResponse"/>.</returns>
        IResponse EndInvoke(IAsyncResult result);
    }

}
