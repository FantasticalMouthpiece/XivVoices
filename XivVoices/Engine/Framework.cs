using System.Collections.Generic;
using System.Threading.Tasks;

namespace XivVoices.Engine
{
    public class Framework
    {
        public Queue<string> Queue { get; set; } = new Queue<string>();
        private bool Active { get; set; } = false;

        public Framework()
        {
        }

        public void Dispose()
        {
            Active = false;
        }


        internal async Task Process(XivMessage xivMessage)
        {
        }

        public async Task Run(string directoryPath, bool once = false)
        {
        }
    }
}
