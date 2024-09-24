namespace Babble_Bot;

public class Message {
    public string Content { get; set; } = "";
    public string Attachment { get; set; } = "";
}

public class ResponseMessage {
    public Message[] Messages = Array.Empty<Message>();
}