// See https://aka.ms/new-console-template for more information

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

 class Packet{
    public string Symbol { get; set; }
    public string Side { get; set; }
    public int Quantity { get; set; }
    public int Price { get; set; }
    public int Sequence { get; set; }
}

class ABXClient
{
    const string ServerIp = "127.0.0.1";
    const int Port = 3000;

    static void Main()
    {
        var allPackets = StreamAllPackets();
        var missingSequences = FindMissingSequences(allPackets);

        Console.WriteLine($"Missing sequences: {string.Join(", ", missingSequences)}");

        var recoveredPackets = RequestMissingPackets(missingSequences);
        foreach (var packet in recoveredPackets)
        {
            allPackets[packet.Sequence] = packet;
        }

        var sortedPackets = SortPacketsBySequence(allPackets);
        WritePacketsToJson(sortedPackets, "output.json");

        Console.WriteLine("✅ JSON file 'output.json' created with all packets in correct sequence.");
    }

    // Step 1: Stream all packets from the server
    static Dictionary<int, Packet> StreamAllPackets()
    {
        var packets = new Dictionary<int, Packet>();

        using var client = new TcpClient(ServerIp, Port);
        using var stream = client.GetStream();

        // Send callType 1 to stream all packets
        stream.Write(new byte[] { 1, 0 });

        byte[] buffer = new byte[17];
        int bytesRead;

        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) == 17)
        {
            var packet = ParsePacket(buffer);
            packets[packet.Sequence] = packet;
        }

        return packets;
    }

    // Step 2: Identify missing sequences
    static List<int> FindMissingSequences(Dictionary<int, Packet> packets)
    {
        int maxSeq = int.MinValue;
        foreach (var seq in packets.Keys)
            if (seq > maxSeq) maxSeq = seq;

        var missing = new List<int>();
        for (int i = 1; i <= maxSeq; i++)
            if (!packets.ContainsKey(i)) missing.Add(i);

        return missing;
    }

    // Step 3: Request missing packets individually
    static List<Packet> RequestMissingPackets(List<int> missingSequences)
    {
        var recoveredPackets = new List<Packet>();

        foreach (int seq in missingSequences)
        {
            using var client = new TcpClient(ServerIp, Port);
            using var stream = client.GetStream();

            stream.Write(new byte[] { 2, (byte)seq });

            byte[] buffer = new byte[17];
            if (stream.Read(buffer, 0, buffer.Length) == 17)
            {
                var packet = ParsePacket(buffer);
                recoveredPackets.Add(packet);
            }
        }

        return recoveredPackets;
    }

    // Step 4: Parse a 17-byte packet
    static Packet ParsePacket(byte[] buffer)
    {
        string symbol = Encoding.ASCII.GetString(buffer, 0, 4);
        string side = Encoding.ASCII.GetString(buffer, 4, 1);
        int quantity = ReadInt32BigEndian(buffer, 5);
        int price = ReadInt32BigEndian(buffer, 9);
        int sequence = ReadInt32BigEndian(buffer, 13);

        return new Packet
        {
            Symbol = symbol,
            Side = side,
            Quantity = quantity,
            Price = price,
            Sequence = sequence
        };
    }

    // Helper to read big-endian integers
    static int ReadInt32BigEndian(byte[] data, int index)
    {
        return (data[index] << 24)
             | (data[index + 1] << 16)
             | (data[index + 2] << 8)
             | data[index + 3];
    }

    // Step 5: Sort packets in order
    static List<Packet> SortPacketsBySequence(Dictionary<int, Packet> packets)
    {
        var sorted = new List<Packet>();
        int maxSeq = packets.Count;

        for (int i = 1; i <= maxSeq; i++)
        {
            if (packets.TryGetValue(i, out var packet))
                sorted.Add(packet);
        }

        return sorted;
    }

    // Step 6: Write to JSON file
    static void WritePacketsToJson(List<Packet> packets, string filename)
    {
        var json = JsonSerializer.Serialize(packets, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filename, json);
    }
}
