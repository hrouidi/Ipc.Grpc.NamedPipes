
using System.Security.Principal;
using System.Threading;

namespace Ipc.Grpc.NamedPipes
{
    public sealed class NamedPipeChannelOptions
    {
        public static readonly NamedPipeChannelOptions Default = new();

#if NETCOREAPP || NETSTANDARD2_1
        /// <summary>
        /// Gets or sets a value indicating whether the client pipe can only connect to a server created by the same
        /// user.
        /// </summary>
        public bool CurrentUserOnly { get; set; }
#endif

        /// <summary>
        /// Gets or sets a value indicating the security impersonation level.
        /// </summary>
        public TokenImpersonationLevel ImpersonationLevel { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the number of milliseconds to wait for the server to respond before the connection times out.
        /// </summary>
        public int ConnectionTimeout { get; set; } = Timeout.Infinite;

    }
}