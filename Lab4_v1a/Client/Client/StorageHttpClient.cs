using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;

namespace Client
{
    public class StorageHttpClient
    {
        public string Addr { get; set; }
        public int RetriesNum { get; set; }
        public HttpClient Client { get; private set; }
        public StorageHttpClient(string addr, int retriesNum = 3)
        {
            Addr = addr;
            RetriesNum = retriesNum;
            Client = new HttpClient();
        }
        public async Task<Contracts.Image[]> GetImagesFromService()
        {
            var response = await Client.GetAsync($"{Addr}/images");
            for(int i = 0; i < RetriesNum && response.StatusCode != System.Net.HttpStatusCode.OK; ++i)
            {
                response = await Client.GetAsync($"{Addr}/images");
            }
            if(response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception($"Failed to get a response from \"{Addr}/images\" in {RetriesNum} attempts");
            }
            var ret = JsonConvert.DeserializeObject<Contracts.Image[]>(response.Content.ReadAsStringAsync().Result);
            if (ret is null)
            {
                return Array.Empty<Contracts.Image>();
            }
            return ret;
        }

        public async Task<Contracts.Image?> GetImageFromService(int id)
        {
            var response = await Client.GetAsync($"{Addr}/images/{id}");
            for (int i = 0; i < RetriesNum && response.StatusCode != System.Net.HttpStatusCode.OK; ++i)
            {
                response = await Client.GetAsync($"{Addr}/images/{id}");
            }
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception($"Failed to get a response from \"{Addr}/images/{id}\" in {RetriesNum} attempts");
            }
            var ret = JsonConvert.DeserializeObject<Contracts.Image>(response.Content.ReadAsStringAsync().Result);
            return ret;
        }

        public async Task<int> PostImageToService(Contracts.Image img)
        {
            var data = new StringContent(JsonConvert.SerializeObject(img), System.Text.Encoding.UTF8, "application/json");
            var response = await Client.PostAsync($"{Addr}/images", data);
            for (int i = 0; i < RetriesNum && response.StatusCode != System.Net.HttpStatusCode.OK; ++i)
            {
                response = await Client.PostAsync($"{Addr}/images", data);
            }
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception($"Failed to get a response from \"{Addr}/images\" in {RetriesNum} attempts");
            }
            var responseString = await response.Content.ReadAsStringAsync();
            return Int32.Parse(responseString);
        }

        public async Task DeleteImagesFromService()
        {
            var response = await Client.DeleteAsync($"{Addr}/images");
            for (int i = 0; i < RetriesNum && response.StatusCode != System.Net.HttpStatusCode.OK; ++i)
            {
                response = await Client.DeleteAsync($"{Addr}/images");
            }
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception($"Failed to get a response from \"{Addr}/images\" in {RetriesNum} attempts");
            }
        }
    }
}
