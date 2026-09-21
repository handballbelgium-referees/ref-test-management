using System.Text.Json;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;

/// <summary>
/// Reads a job's JSON payload into its typed shape.
/// </summary>
/// <remarks>
/// Shared by every handler rather than duplicated into each, because the two things it does beyond
/// calling <see cref="JsonSerializer"/> are both easy to leave out and expensive to leave out:
/// classifying an unreadable payload as terminal, and keeping the participant's details out of the
/// log when it is.
/// </remarks>
public static class JobPayload
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <exception cref="JobPayloadException">
    /// The payload is absent or cannot be read. Thrown rather than <see cref="JsonException"/> so
    /// the caller can tell "this will never work" apart from "this might work next time".
    /// </exception>
    public static T Deserialize<T>(Job job, ILogger logger) where T : IJobPayload
    {
        // A cancelled job has its payload cleared, and a job that was cancelled while a worker
        // held it can still reach a handler. Treat that as terminal rather than letting
        // JsonException burn every retry attempt.
        if (string.IsNullOrEmpty(job.Payload))
            throw new JobPayloadException($"Job {job.Id} has no payload to deserialize");

        try
        {
            var payload = JsonSerializer.Deserialize<T>(job.Payload, SerializerOptions);

            return payload ?? throw new JobPayloadException($"Job {job.Id} deserialized to a null payload");
        }
        catch (JsonException ex)
        {
            // The payload carries the participant's name and email; a deserialization failure
            // can quote the offending fragment back in its message.
            ServiceLoggerMessages.LogJobDeserializationError(logger, LogRedaction.MaskEmails(ex), job.Id);
            throw new JobPayloadException($"Job {job.Id} has a payload that could not be deserialized", ex);
        }
    }
}
