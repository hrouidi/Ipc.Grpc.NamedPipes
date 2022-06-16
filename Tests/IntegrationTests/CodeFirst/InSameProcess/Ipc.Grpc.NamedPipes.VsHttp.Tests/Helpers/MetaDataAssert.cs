using System.Collections.Generic;
using System.Linq;
using Grpc.Core;
using NUnit.Framework;

namespace Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers;

public static class MetadataAssert
{
    public static void AreEquivalent(Metadata expected, Metadata actual)
    {
        //Assert.That(expected.Count,Is.EqualTo(actual.Count));

        Dictionary<string, Metadata.Entry> actualDict = actual.ToDictionary(x => x.Key);
        foreach (Metadata.Entry expectedEntry in expected)
        {
            Assert.True(actualDict.ContainsKey(expectedEntry.Key));
            Metadata.Entry actualEntry = actualDict[expectedEntry.Key];
            Assert.That(actualEntry.IsBinary, Is.EqualTo(expectedEntry.IsBinary));
            if (expectedEntry.IsBinary)
                Assert.That(actualEntry.ValueBytes.AsEnumerable(), Is.EqualTo(expectedEntry.ValueBytes.AsEnumerable()));
            else
                Assert.That(actualEntry.Value, Is.EqualTo(expectedEntry.Value));
        }
    }
}