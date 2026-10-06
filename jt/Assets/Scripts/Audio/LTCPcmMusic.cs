using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
namespace LTC.Audio {
public static class LTCPcmMusic {
    static readonly Dictionary<string, AudioClip> cache = new();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { cache.Clear(); }
    public static AudioClip Load(string name) {
        if (cache.TryGetValue(name, out var clip) && clip) return clip;
        var asset = Resources.Load<TextAsset>("LTCAudio/" + name);
        if (!asset) return null;
        var b = asset.bytes;
        if (b.Length < 44 || Encoding.ASCII.GetString(b,0,4)!="RIFF" || Encoding.ASCII.GetString(b,8,4)!="WAVE") return null;
        int channels=0, rate=0, bits=0, format=0, offset=0, length=0;
        for(int p=12;p+8<=b.Length;) {
            var tag=Encoding.ASCII.GetString(b,p,4); int size=BitConverter.ToInt32(b,p+4);
            if(size<0 || (long)p+8+size>b.Length) return null;
            if(tag=="fmt " && size>=16) { format=BitConverter.ToUInt16(b,p+8); channels=BitConverter.ToUInt16(b,p+10); rate=BitConverter.ToInt32(b,p+12); bits=BitConverter.ToUInt16(b,p+22); }
            if(tag=="data") {offset=p+8;length=size;}
            p+=8+size+(size&1);
        }
        if(format!=1 || bits!=16 || channels<1 || rate<1 || length<2*channels) return null;
        var samples=new float[length/2];
        for(int i=0;i<samples.Length;i++) samples[i]=BitConverter.ToInt16(b,offset+i*2)/32768f;
        clip=AudioClip.Create(name, samples.Length/channels, channels, rate, false);
        clip.SetData(samples,0); cache[name]=clip; return clip;
    }
}
}