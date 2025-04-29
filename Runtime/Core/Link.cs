using AlienSignals.Runtime.Core.Interfaces;

namespace AlienSignals.Runtime.Core
{
    public class Link
    {
        public IDependency Dep { get; set; }
        public ISubscriber Sub { get; set; }
        public Link PrevSub { get; set; }
        public Link NextSub { get; set; }
        public Link NextDep { get; set; }
    }
}