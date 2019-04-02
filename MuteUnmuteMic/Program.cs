using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MuteUnmuteMic
{
    class Program
    {
        static void Main(string[] args)
        {
            var client = new NamedPipeClientStream(".", "ControlMicPipe", PipeDirection.Out);
            client.Connect();
            using (StreamWriter writer = new StreamWriter(client))
            {
                writer.WriteLine("ToggleMic");
            }
        }
    }
}
