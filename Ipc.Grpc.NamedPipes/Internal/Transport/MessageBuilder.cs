using System;
using System.Collections.Generic;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.TransportProtocol;


namespace Ipc.Grpc.NamedPipes.Internal
{
    internal static class MessageBuilder
    {
        #region Client messages

        //TODO : cache bytes of these messages
        public static Message CancelRequest => new() { RequestControl = Control.Cancel };
        public static Message StreamEnd => new() { RequestControl = Control.StreamMessageEnd };

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

        public static (Message message, byte[] payload) BuildStreamPayload<TRequest>(Marshaller<TRequest> marshaller, TRequest message)
        {
            byte[] payload = SerializationHelpers.Serialize(marshaller, message);
            Message ret = new()
            {
                RequestControl = Control.StreamMessage
            };
            return (ret, payload);
        }

        #endregion

        public static Message BuildResponseHeadersMessage(Metadata responseHeaders)
        {
            Headers headers = ToHeaders(responseHeaders);
            return new Message { ResponseHeaders = headers };
        }

        public static Trailers BuildTrailers(Metadata contextTrailers, StatusCode statusCode, string statusDetail)
        {
            Trailers ret = ToTrailers(contextTrailers);
            ret.StatusCode = (int)statusCode;
            ret.StatusDetail = statusDetail;
            return ret;
        }

        private static Headers ToHeaders(Metadata metadata)
        {
            var headers = new Headers();
            AddMetadata(metadata, headers.Metadata);
            return headers;

        }

        private static Trailers ToTrailers(Metadata metadata)
        {
            var ret = new Trailers();
            AddMetadata(metadata, ret.Metadata);
            return ret;

        }

        private static void AddMetadata(Metadata metadata, ICollection<MetadataEntry> accumulator)
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

        public static Metadata ToMetadata(RepeatedField<MetadataEntry> entries)
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