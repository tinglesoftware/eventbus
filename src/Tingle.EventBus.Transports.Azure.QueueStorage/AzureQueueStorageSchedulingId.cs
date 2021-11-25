using Azure.Storage.Queues.Models;
using System.Diagnostics.CodeAnalysis;

namespace Tingle.EventBus.Transports.Azure.QueueStorage;

internal record AzureQueueStorageSchedulingId
{
    private const string Separator = "|";

    public AzureQueueStorageSchedulingId(string messageId, string popReceipt)
    {
        MessageId = messageId ?? throw new ArgumentNullException(nameof(messageId));
        PopReceipt = popReceipt ?? throw new ArgumentNullException(nameof(popReceipt));
    }

    public string MessageId { get; set; }
    public string PopReceipt { get; set; }

    /// <summary>
    /// Convert <see cref="AzureQueueStorageSchedulingId"/> to <see cref="string"/>.
    /// </summary>
    /// <param name="id"></param>
    public static implicit operator string(AzureQueueStorageSchedulingId id) => string.Join(Separator, id.MessageId, id.PopReceipt);

    /// <summary>
    /// Convert <see cref="QueueMessage"/> to <see cref="AzureQueueStorageSchedulingId"/>.
    /// </summary>
    /// <param name="message"></param>
    public static implicit operator AzureQueueStorageSchedulingId(QueueMessage message) => new(message.MessageId, message.PopReceipt);

    /// <summary>
    /// Convert <see cref="SendReceipt"/> to <see cref="AzureQueueStorageSchedulingId"/>.
    /// </summary>
    /// <param name="receipt"></param>
    public static implicit operator AzureQueueStorageSchedulingId(SendReceipt receipt) => new(receipt.MessageId, receipt.PopReceipt);

    public static bool TryParse(string id, [NotNullWhen(true)] out AzureQueueStorageSchedulingId? sid)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException($"'{nameof(id)}' cannot be null or whitespace.", nameof(id));
        }

        sid = null;
        var parts = id.Split(Separator);
        if (parts.Length != 2) return false;

        sid = new AzureQueueStorageSchedulingId(messageId: parts[0], popReceipt: parts[1]);
        return true;
    }

    /// <summary>
    /// Deconstruct the Id into parts
    /// </summary>
    /// <param name="messageId">See <see cref="MessageId"/>.</param>
    /// <param name="popReceipt">See <see cref="PopReceipt"/>.</param>
    public void Deconstruct(out string messageId, out string popReceipt)
    {
        messageId = MessageId;
        popReceipt = PopReceipt;
    }
}
