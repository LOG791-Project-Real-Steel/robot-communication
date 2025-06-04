using Newtonsoft.Json;

namespace RemoteKeyboardController.Models
{
    public abstract record Message(string Type)
    {
        public virtual string Json()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
