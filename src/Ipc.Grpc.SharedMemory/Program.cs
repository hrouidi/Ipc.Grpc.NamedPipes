using System.IO.MemoryMappedFiles;

namespace Ipc.Grpc.SharedMemory
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Debug();


            Console.ReadKey();
        }

        public static void Debug()
        {
            using var mmf = MemoryMappedFile.CreateNew("test", 1024);

            using MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor();

            Span<byte> viewSpan = GetSpan(accessor);

            var buffer1 = new byte[1024 * 4];
            Random.Shared.NextBytes(viewSpan);
            // accessor.WriteArray(0, buffer1, 0, 1024);


            using var mmf2 = MemoryMappedFile.OpenExisting("test");
            using MemoryMappedViewAccessor accessor2 = mmf2.CreateViewAccessor();
            var buffer = new byte[1024];
            int count = accessor2.ReadArray(0, buffer, 0, 1024);

            //ref MemoryMappedViewAccessor tmp = ref Unsafe.AsRef(accessor);


        }

        public static unsafe Span<byte> GetSpan(MemoryMappedViewAccessor accessor)
        {
            byte* ptr = null;
            try
            {
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
                Span<byte> buffer2 = new(ptr, (int)accessor.Capacity);
                return buffer2;
            }
            finally
            {
                accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            }
        }
    }
}
