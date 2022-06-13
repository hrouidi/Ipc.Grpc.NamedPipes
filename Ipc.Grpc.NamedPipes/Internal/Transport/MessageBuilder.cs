using System;
using System.Collections.Generic;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Ipc.Grpc.NamedPipes.Internal.Transport
{
    internal static class MessageBuilder
    {
        #region Client messages

        public static Message CancelRequest { get; } = new() { RequestControl = Control.Cancel };

        public static Message StreamEnd { get; } = new() { RequestControl = Control.StreamMessageEnd };

        public static Message BuildRequest<TRequest, TResponse>(Method<TRequest, TResponse> method, DateTime? deadline, Metadata headers)
        {
            Message message = new()
            {
                Request = new Request
                {
                    MethodFullName = method.FullName,
                    MethodType = (Request.Types.MethodType)method.Type,
                    Deadline = deadline != null ? Timestamp.FromDateTime(deadline.Value) : null,
                    Headers = ToHeaders(headers),
                }
            };
            return message;
        }

        #endregion

        public static Message Streaming { get; } = new() { RequestControl = Control.StreamMessage };

        #region Server messages

        public static Message BuildResponseHeaders(Metadata responseHeaders)
        {
            Headers headers = ToHeaders(responseHeaders);
            return new Message { ResponseHeaders = headers };
        }

        public static Message BuildReply(Metadata trailers, StatusCode statusCode, string statusDetail)
        {
            Reply ret = new();
            EncodeMetadata(trailers, ret.Trailers);
            ret.StatusCode = (int)statusCode;
            ret.StatusDetail = statusDetail;

            Message message = new() { Response = ret };
            return message;
        }

        #endregion

        private static Headers ToHeaders(Metadata metadata)
        {
            var headers = new Headers();
            EncodeMetadata(metadata, headers.Metadata);
            return headers;
        }

        private static void EncodeMetadata(Metadata metadata, ICollection<MetadataEntry> accumulator)
        {
            foreach (Metadata.Entry entry in metadata ?? new Metadata())
            {
                var transportEntry = new MetadataEntry { Name = entry.Key };
                if (entry.IsBinary)
                    transportEntry.ValueBytes = ByteString.CopyFrom(entry.ValueBytes);
                else
                    transportEntry.ValueString = entry.Value;

                accumulator.Add(transportEntry);
            }

        }

        public static Metadata DecodeMetadata(RepeatedField<MetadataEntry> entries)
        {
            var metadata = new Metadata();
            foreach (MetadataEntry entry in entries)
            {
                switch (entry.ValueCase)
                {
                    case MetadataEntry.ValueOneofCase.ValueString:
                        metadata.Add(new Metadata.Entry(entry.Name, entry.ValueString));
                        break;
                    case MetadataEntry.ValueOneofCase.ValueBytes:
                        metadata.Add(new Metadata.Entry(entry.Name, entry.ValueBytes.ToByteArray()));
                        break;
                }
            }

            return metadata;
        }
    }
}