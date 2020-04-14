using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;

public class IRCbot
{
    // server to connect to (edit at will)
    public static string SERVER = "irc.changeme.com";
    // server port (6667 by default)
    private static int PORT = 6667;
    // user information defined in RFC 2812 (IRC: Client Protocol) is sent to the IRC server 
    private static string USER = "USER IRCbot 0 * :IRCbot";
    // the bot's nickname
    private static string NICK = "IRCbot";
    // channel to join
    private static string CHANNEL = "#opers";

    static void Main(string[] args)
    {
        NetworkStream stream;
        TcpClient irc;
        string inputLine;
        StreamReader reader;
        StreamWriter writer;

        try
        {
            irc = new TcpClient(SERVER, PORT);
            stream = irc.GetStream();
            reader = new StreamReader(stream);
            writer = new StreamWriter(stream);
            writer.WriteLine("NICK " + NICK);
            writer.Flush();
            writer.WriteLine(USER);
            writer.Flush();

            while (true)
            {
                while ((inputLine = reader.ReadLine()) != null)
                {
                    Console.WriteLine("<- " + inputLine);

                    // split the lines sent from the server by spaces (seems to be the easiest way to parse them)
                    string[] splitInput = inputLine.Split(new Char[] {
                        ' '
                    });

                    if (splitInput[0] == "PING")
                    {
                        string PongReply = splitInput[1];
                        //Console.WriteLine("->PONG " + PongReply);
                        writer.WriteLine("PONG " + PongReply);
                        writer.Flush();
                        continue;
                    }

                    switch (splitInput[1])
                    {
                        case "001":
                            string JoinString = "JOIN " + CHANNEL;
                            writer.WriteLine(JoinString);
                            writer.Flush();
                            break;
                        default:
                            break;
                    }
                }

                // close all streams (to preserve memory)
                writer.Close();
                reader.Close();
                irc.Close();
            }
        }
        catch (Exception e)
        {
            // shows the exception, sleeps for a little while and then tries to establish a new connection to the IRC server
            Console.WriteLine(e.ToString());
            Thread.Sleep(5000);
            string[] argv = {};
            Main(argv);
        }
    }
}
