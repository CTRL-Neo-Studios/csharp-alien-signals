using AlienSignals.Core.Interfaces;

namespace AlienSignals.Core
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