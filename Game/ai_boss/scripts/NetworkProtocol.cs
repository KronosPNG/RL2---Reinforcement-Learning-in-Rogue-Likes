using System;
using System.IO;
using Godot;

/// <summary>
/// Wire protocol for the boss-AI socket connection.
/// Every message sent over the socket is: [1 byte NetMsgType][payload...]
/// StaticState/DynamicState payloads come from GameStateSerializer;
/// Outcome/Command/Action payloads are (de)serialized here.
/// </summary>
public enum NetMsgType : byte
{
    StaticState = 0,
    DynamicState = 1,
    Outcome = 2,
    Command = 3,
    Action = 10
}

/// <summary>
/// Commands sent from the Godot client to the AI server to control its lifecycle.
/// </summary>
public enum CommandType : byte
{
    Restart = 0,      // Ask the server to reset/restart for a new episode
    CloseSession = 1  // Ask the server to close the connection and shut down
}

/// <summary>
/// Action chosen by the AI server, sent back to the client.
/// </summary>
public struct AiAction
{
    public float X;
    public float Y;
    public int ActionId;
}

public static class NetworkProtocol
{
    public const int OutcomeSize = 1;
    public const int CommandSize = 1;
    public const int AiActionSize = 4 + 4 + 4; // X, Y, ActionId

    // ===================== Framing =====================

    /// <summary>Prepend a 1-byte type header to a payload.</summary>
    public static byte[] BuildMessage(NetMsgType type, byte[] payload)
    {
        byte[] msg = new byte[1 + payload.Length];
        msg[0] = (byte)type;
        Buffer.BlockCopy(payload, 0, msg, 1, payload.Length);
        return msg;
    }

    /// <summary>Build a message with no payload (header byte only).</summary>
    public static byte[] BuildMessage(NetMsgType type)
    {
        return new byte[] { (byte)type };
    }

    /// <summary>Split an incoming packet into its type header and remaining payload.</summary>
    public static (NetMsgType type, byte[] payload) ParseMessage(byte[] data)
    {
        if (data == null || data.Length < 1)
        {
            throw new ArgumentException("Packet is empty; cannot contain a message header.");
        }

        var type = (NetMsgType)data[0];
        byte[] payload = new byte[data.Length - 1];
        Buffer.BlockCopy(data, 1, payload, 0, payload.Length);
        return (type, payload);
    }

    // ===================== Outcome =====================

    public static byte[] SerializeOutcome(bool won)
    {
        return new byte[] { won ? (byte)1 : (byte)0 };
    }

    public static bool DeserializeOutcome(byte[] data)
    {
        return data[0] != 0;
    }

    // ===================== Command =====================

    public static byte[] SerializeCommand(CommandType cmd)
    {
        return new byte[] { (byte)cmd };
    }

    public static CommandType DeserializeCommand(byte[] data)
    {
        return (CommandType)data[0];
    }

    // ===================== AiAction =====================

    public static byte[] SerializeAction(AiAction action)
    {
        using var ms = new MemoryStream(AiActionSize);
        using var w = new BinaryWriter(ms);

        w.Write(action.X);
        w.Write(action.Y);
        w.Write(action.ActionId);

        return ms.ToArray();
    }

    public static AiAction DeserializeAction(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var r = new BinaryReader(ms);

        return new AiAction
        {
            X = r.ReadSingle(),
            Y = r.ReadSingle(),
            ActionId = r.ReadInt32()
        };
    }
}
