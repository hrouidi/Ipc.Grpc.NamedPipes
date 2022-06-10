using System.IO.Pipes;

namespace Ipc.Grpc.NamedPipes
{
    public sealed class NamedPipeServerOptions
    {
        public static readonly NamedPipeServerOptions Default = new ();

#if NETCOREAPP || NETSTANDARD2_1
        /// <summary>
        /// Gets or sets a value indicating whether the server pipe can only be connected to a client created by the
        /// same user.
        /// </summary>
        public bool CurrentUserOnly { get; set; }
#endif
#if NETFRAMEWORK || NET5_0
        /// <summary>
        /// Gets or sets a value indicating the access control to be used for the pipe.
        /// </summary>
        public PipeSecurity PipeSecurity { get; set; }
#endif
        // TODO:  Add fault resilient strategies (retry,circuit breaker..)
    }
}