using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Numerics;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SignalWorkbench {
public class Settings {
 public int Version=1, Mode=0, N=1024, Depth=1024, Width=16, Fraction=15, Radix=16;
 public string Window="Hann", Wave="Sine", Normalization="Raw", Encoding="Signed", Rounding="Even", Overflow="Error", Platform="Vivado", AddWindow="None";
 public bool Periodic=false, FrequencyMode=false;
 public double Parameter=8.6, Gain=0.999969482421875, Minimum=-0.8, Maximum=0.8, Cycles=4, Frequency=4000, SampleRate=1024000, Phase=0, Duty=0.5;
 public Settings Copy() {return (Settings)MemberwiseClone();}
 public void Save(string path) {using(var w=new StreamWriter(path,false,new UTF8Encoding(false)))new XmlSerializer(typeof(Settings)).Serialize(w,this);}
 public static Settings Load(string path) {using(var r=System.Xml.XmlReader.Create(path,new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit}))return (Settings)new XmlSerializer(typeof(Settings)).Deserialize(r);}
}
public class Result {
 public Settings Config; public double[] Values,Decoded; public long[] Integers; public ulong[] Words;
 public int Saturated; public double MaxError,RmsError,Min,Max,Mean,Rms; public string Notes;
}
public static class Engine {
 public static readonly CultureInfo CI=CultureInfo.InvariantCulture;
 public static readonly string[] Windows={"Rectangular","Hann","Hamming","Blackman","BlackmanHarris","Nuttall","Bartlett","BartlettHann","FlatTop","Kaiser","Tukey","Gaussian"};
 public static readonly string[] Waves={"Sine","Cosine","Triangle","Square","SawUp","SawDown","DC","Pulse"};
 public static string F(double v){return v.ToString("G17",CI);}
 static void Require(bool yes,string why){if(!yes)throw new ArgumentException(why);}
 public static bool Finite(double v){return !double.IsNaN(v)&&!double.IsInfinity(v);}
 public static void Validate(Settings s){
  Require(s.Version==1,"参数文件版本不兼容。");Require(s.Mode==0||s.Mode==1,"滤波器系数生成尚未开放。");
  Require(s.N>=2&&s.N<=1048576,"点数必须为 2～1,048,576。");Require(s.Depth>=s.N&&s.Depth<=1048576,"存储深度必须不小于点数，且不超过 1,048,576。");
  Require(s.Width>=1&&s.Width<=32,"位宽必须为 1～32 bit。");Require(new[]{"Signed","Unsigned","Offset"}.Contains(s.Encoding),"未知编码。");
  Require(s.Fraction>=0&&s.Fraction<=(s.Encoding=="Unsigned"?s.Width:s.Width-1),"小数位超出当前位宽/编码允许范围。");
  Require(new[]{2,10,16}.Contains(s.Radix),"进制只支持 2、10、16。");Require(new[]{"Vivado","Altera"}.Contains(s.Platform),"未知平台。");
  Require(new[]{"Raw","Peak","Sum","RMS"}.Contains(s.Normalization),"未知归一化方式。");Require(new[]{"Even","Truncate","Floor"}.Contains(s.Rounding),"未知舍入方式。");Require(s.Overflow=="Error"||s.Overflow=="Saturate","未知溢出策略。");
  Require(Windows.Contains(s.Window),"未知窗函数。");Require(Waves.Contains(s.Wave),"未知波形。");Require(s.AddWindow=="None"||Windows.Contains(s.AddWindow),"未知加窗类型。");
  foreach(double v in new[]{s.Parameter,s.Gain,s.Minimum,s.Maximum,s.Cycles,s.Frequency,s.SampleRate,s.Phase,s.Duty})Require(Finite(v),"参数不可为 NaN 或无穷大。");
  Require(s.Gain>=0&&s.Gain<=1e9,"窗增益应在 0～1e9。");Require(s.SampleRate>0&&s.SampleRate<=1e12,"采样率必须在 (0, 1e12] Hz。");
  if(s.Mode==0)ValidateWindowParameter(s.Window,s.Parameter);
  else {
   Require(s.Minimum<=s.Maximum&&Math.Abs(s.Minimum)<=1e12&&Math.Abs(s.Maximum)<=1e12,"最小值不得大于最大值，且绝对值不得超过 1e12。");
   Require(s.Phase>=-360000&&s.Phase<=360000,"相位应在 ±360000° 内。");
   if(s.Wave!="DC"){
    double c=s.FrequencyMode?s.Frequency*s.N/s.SampleRate:s.Cycles;
    Require(Finite(c)&&c>0&&c<s.N/2.0,"基波频率必须大于 0 且严格小于 Fs/2；请降低频率/周期数或提高点数/采样率。");
   }
   if(s.Wave=="Square"||s.Wave=="Pulse"||s.Wave=="Triangle")Require(s.Duty>0&&s.Duty<1,"占空比/上升段比例必须在 (0, 1) 内。");
   if(s.AddWindow!="None")ValidateWindowParameter(s.AddWindow,s.Parameter);
  }
 }
 static void ValidateWindowParameter(string kind,double p){
  if(kind=="Kaiser")Require(p>=0&&p<=50,"Kaiser β 支持 0～50。");
  if(kind=="Tukey")Require(p>=0&&p<=1,"Tukey α 必须在 0～1。");
  if(kind=="Gaussian")Require(p>0&&p<=1e9,"Gaussian σ 必须在 (0, 1e9]，单位为样本。");
 }
 // I0 power series: positive terms, relative termination, validated beta range <=50.
 static double I0(double x){double sum=1,term=1,z=x*x/4;for(int k=1;k<500;k++){term*=z/(k*(double)k);sum+=term;if(term<=sum*2e-16)break;}return sum;}
 public static double[] Window(string kind,int n,bool periodic,double p){
  Require(Windows.Contains(kind),"未知窗函数。");Require(n>=2&&n<=1048576,"窗长度超出范围。");ValidateWindowParameter(kind,p);
  double d=periodic?n:n-1; double[] a=null;
  switch(kind){case "Rectangular":a=new[]{1.0};break;case "Hann":a=new[]{0.5,0.5};break;case "Hamming":a=new[]{0.54,0.46};break;case "Blackman":a=new[]{0.42,0.5,0.08};break;
   case "BlackmanHarris":a=new[]{0.35875,0.48829,0.14128,0.01168};break;case "Nuttall":a=new[]{0.3635819,0.4891775,0.1365995,0.0106411};break;
   case "FlatTop":a=new[]{0.21557895,0.41663158,0.277263158,0.083578947,0.006947368};break;}
  var w=new double[n]; double denom=kind=="Kaiser"?I0(p):1;
  for(int i=0;i<n;i++){
   double t=i/d,theta=2*Math.PI*t,v=0;
   if(a!=null){for(int k=0;k<a.Length;k++)v+=(k%2==0?1:-1)*a[k]*Math.Cos(k*theta);}
   else if(kind=="Bartlett")v=1-Math.Abs(2*t-1);
   else if(kind=="BartlettHann")v=0.62-0.48*Math.Abs(t-0.5)-0.38*Math.Cos(theta);
   else if(kind=="Kaiser")v=I0(p*Math.Sqrt(Math.Max(0,1-(2*t-1)*(2*t-1))))/denom;
   else if(kind=="Gaussian")v=Math.Exp(-0.5*Math.Pow((i-d/2)/p,2));
   else if(kind=="Tukey"){
    if(p==0)v=1;else if(t<p/2)v=0.5*(1+Math.Cos(Math.PI*(2*t/p-1)));else if(t>1-p/2)v=0.5*(1+Math.Cos(Math.PI*(2*t/p-2/p+1)));else v=1;
   }
   // Cancel roundoff at analytically zero endpoints; preserve real negative lobes.
   if((i==0||(!periodic&&i==n-1))&&Math.Abs(v)<2e-15&&
      (kind=="Hann"||kind=="Blackman"||kind=="Bartlett"||kind=="BartlettHann"||(kind=="Tukey"&&p>0)))v=0;
   w[i]=v;
  } return w;
 }
 public static void Normalize(double[] w,string mode){
  double norm=1;
  if(mode=="Peak")norm=w.Max(x=>Math.Abs(x));else if(mode=="Sum")norm=w.Sum();else if(mode=="RMS")norm=Math.Sqrt(w.Sum(x=>x*x)/w.Length);else if(mode!="Raw")throw new ArgumentException("未知归一化。");
  Require(Finite(norm)&&Math.Abs(norm)>1e-300,"该序列的归一化分母为零或过小；请改用原始系数或增大点数。");
  for(int i=0;i<w.Length;i++)w[i]/=norm;
 }
 public static double[] GenerateValues(Settings s){
  Validate(s); double[] x;
  if(s.Mode==0){x=Window(s.Window,s.N,s.Periodic,s.Parameter);Normalize(x,s.Normalization);for(int i=0;i<x.Length;i++)x[i]*=s.Gain;}
  else{
   x=new double[s.N];double cycles=s.FrequencyMode?s.Frequency*s.N/s.SampleRate:s.Cycles;
   for(int i=0;i<x.Length;i++){
    double t=cycles*i/s.N+s.Phase/360; t-=Math.Floor(t);double unit=0;
    switch(s.Wave){
     case "Sine":unit=(1+Math.Sin(2*Math.PI*t))/2;break;case "Cosine":unit=(1+Math.Cos(2*Math.PI*t))/2;break;
     case "Triangle":unit=t<s.Duty?t/s.Duty:(1-t)/(1-s.Duty);break;
     case "Square":case "Pulse":unit=t<s.Duty?1:0;break;
     case "SawUp":unit=t;break;case "SawDown":unit=1-t;break;case "DC":unit=1;break;
    }
    x[i]=s.Minimum+(s.Maximum-s.Minimum)*unit;
   }
   if(s.AddWindow!="None"){var w=Window(s.AddWindow,s.N,s.Periodic,s.Parameter);Normalize(w,s.Normalization);for(int i=0;i<x.Length;i++)x[i]*=w[i]*s.Gain;}
  } return x;
 }
 public static long MinCode(Settings s){return s.Encoding=="Unsigned"?0:-(1L<<(s.Width-1));}
 public static long MaxCode(Settings s){return s.Encoding=="Unsigned"?(1L<<s.Width)-1:(1L<<(s.Width-1))-1;}
 public static Result Quantize(Settings s,double[] x){
  Validate(s);Require(x.Length==s.N,"输入序列长度不符。");var r=new Result{Config=s.Copy(),Values=(double[])x.Clone(),Decoded=new double[s.Depth],Integers=new long[s.Depth],Words=new ulong[s.Depth]};
  double scale=Math.Pow(2,s.Fraction),err2=0,sum=0,squares=0;long lo=MinCode(s),hi=MaxCode(s);ulong mask=(1UL<<s.Width)-1;
  r.Min=double.PositiveInfinity;r.Max=double.NegativeInfinity;
  for(int i=0;i<s.Depth;i++){
   double v=i<x.Length?x[i]:0;Require(Finite(v),"序列包含无效数值。");double q=v*scale;
   q=s.Rounding=="Even"?Math.Round(q,MidpointRounding.ToEven):s.Rounding=="Floor"?Math.Floor(q):Math.Truncate(q);
   if(!Finite(q)||q<lo||q>hi){
    if(s.Overflow=="Error")throw new ArgumentException("第 "+i+" 点量化溢出（值="+F(v)+"）。允许范围 ["+F(lo/scale)+", "+F(hi/scale)+"]。请降低增益/幅值、减小小数位，或明确选择饱和。");
    r.Saturated++;q=q<lo?lo:hi;
   }
   long code=(long)q;r.Integers[i]=code;r.Decoded[i]=code/scale;r.Words[i]=s.Encoding=="Offset"?(ulong)(code+(1L<<(s.Width-1))):unchecked((ulong)code)&mask;
   if(i<x.Length){double e=r.Decoded[i]-v;r.MaxError=Math.Max(r.MaxError,Math.Abs(e));err2+=e*e;sum+=v;squares+=v*v;r.Min=Math.Min(r.Min,v);r.Max=Math.Max(r.Max,v);}
  }
  r.RmsError=Math.Sqrt(err2/x.Length);r.Mean=sum/x.Length;r.Rms=Math.Sqrt(squares/x.Length);
  var notes=new List<string>();notes.Add("补零表示数值 0；偏移二进制对应中点码。统计仅包含 N 个有效样本。");
  if(s.Mode==1&&s.Wave!="DC"){
   double cycles=s.FrequencyMode?s.Frequency*s.N/s.SampleRate:s.Cycles;
   notes.Add("f="+F(cycles*s.SampleRate/s.N)+" Hz；记录周期数="+F(cycles)+"；t[n]=n/Fs，不重复终点。");
   if(Math.Abs(cycles-Math.Round(cycles))>1e-10)notes.Add("非整数周期：循环播放可能出现边界跳变。");
   if(s.Wave!="Sine"&&s.Wave!="Cosine")notes.Add("理想非正弦波未经带限，谐波可能混叠；不能视作无混叠模拟波形。");
  }
  if(r.Saturated>0)notes.Add("发生 "+r.Saturated+" 点饱和，导出前需确认。");
  r.Notes=string.Join("\r\n",notes);return r;
 }
 public static Result Generate(Settings s){return Quantize(s,GenerateValues(s));}
 public static string Word(ulong word,int radix,int width){if(radix==16)return word.ToString("X"+((width+3)/4),CI);if(radix==2)return Convert.ToString((long)word,2).PadLeft(width,'0');return word.ToString(CI);}
 public static void WriteMemory(TextWriter w,Result r,int limit){
  var s=r.Config;bool coe=s.Platform=="Vivado";string c=coe?"; ":"-- ";
  w.WriteLine(c+"SignalWorkbench 1.1.1 | ROM/RAM initialization, NOT FIR Compiler coefficients");
  w.WriteLine(c+"N="+s.N+" DEPTH="+s.Depth+" WIDTH="+s.Width+" FRACTION="+s.Fraction+" ENCODING="+s.Encoding);
  if(s.Mode==2)w.WriteLine(c+"SOURCE="+s.Window+" COEFFICIENT_SCALE=2^F; IIR feedback subtracts a1,a2");else w.WriteLine(c+"SOURCE="+(s.Mode==0?s.Window:s.Wave)+" PERIODIC="+s.Periodic+" NORMALIZATION="+s.Normalization+" GAIN="+F(s.Gain));
  w.WriteLine(c+"ROUNDING="+s.Rounding+" OVERFLOW="+s.Overflow+" SATURATED="+r.Saturated+" MAX_ABS_ERROR="+F(r.MaxError));
  if(coe){w.WriteLine("memory_initialization_radix="+s.Radix+";");w.WriteLine("memory_initialization_vector=");}
  else{w.WriteLine("WIDTH="+s.Width+";\nDEPTH="+s.Depth+";\nADDRESS_RADIX=UNS;\nDATA_RADIX="+(s.Radix==16?"HEX":s.Radix==2?"BIN":"UNS")+";\nCONTENT BEGIN");}
  int count=Math.Min(r.Words.Length,limit);
  for(int i=0;i<count;i++){string v=Word(r.Words[i],s.Radix,s.Width);w.WriteLine(coe?v+(i==r.Words.Length-1?";":","):i+" : "+v+";");}
  if(count<r.Words.Length)w.WriteLine(c+"PREVIEW TRUNCATED: "+count+" / "+r.Words.Length+" words. Export writes ALL words.");
  else if(!coe)w.WriteLine("END;");
 }
 public static string Preview(Result r){using(var w=new StringWriter(CI)){WriteMemory(w,r,128);return w.ToString();}}
 public static void Export(string path,Result r){
  string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{using(var w=new StreamWriter(temp,false,Encoding.ASCII))WriteMemory(w,r,int.MaxValue);VerifyFile(temp,r);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}
 }
 public static void VerifyFile(string path,Result expected){
  var values=new List<ulong>();int radix=0;bool data=false;var s=expected.Config;
  foreach(var raw in File.ReadLines(path)){
   string line=raw.Trim();if(line.Length==0||line.StartsWith(";")||line.StartsWith("--"))continue;
   if(s.Platform=="Vivado"){
    if(line.StartsWith("memory_initialization_radix="))radix=int.Parse(line.Split('=')[1].TrimEnd(';'),CI);
    else if(line=="memory_initialization_vector=")data=true;
    else if(data)values.Add(Convert.ToUInt64(line.TrimEnd(',',';'),radix));
   }else{
    if(line.StartsWith("DATA_RADIX=")){string v=line.Split('=')[1];radix=v=="HEX;"?16:v=="BIN;"?2:10;}
    else if(line=="CONTENT BEGIN")data=true;
    else if(data&&line!="END;"){var parts=line.Split(':');Require(int.Parse(parts[0],CI)==values.Count,"MIF 地址不连续。");values.Add(Convert.ToUInt64(parts[1].Trim().TrimEnd(';'),radix));}
   }
  }
  Require(radix==s.Radix&&values.Count==s.Depth,"导出回读：进制或深度不符。");for(int i=0;i<values.Count;i++)Require(values[i]==expected.Words[i],"导出回读第 "+i+" 点不符。");
 }
 public static void ExportCsv(string path,Result r){using(var w=new StreamWriter(path,false,new UTF8Encoding(true))){w.WriteLine("address,is_padding,source_value,decoded_value,integer_value,raw_word_hex");for(int i=0;i<r.Words.Length;i++)w.WriteLine(i+","+(i>=r.Config.N?"true":"false")+","+(i<r.Config.N?F(r.Values[i]):"0")+","+F(r.Decoded[i])+","+r.Integers[i]+","+Word(r.Words[i],16,r.Config.Width));}}
 public static double[] Spectrum(double[] source,out int fftLength){
  int m=1;while(m<source.Length)m<<=1;while(m<4096)m<<=1;fftLength=m;var a=new Complex[m];for(int i=0;i<source.Length;i++)a[i]=new Complex(source[i],0);
  for(int i=1,j=0;i<m;i++){int bit=m>>1;for(;(j&bit)!=0;bit>>=1)j^=bit;j^=bit;if(i<j){var t=a[i];a[i]=a[j];a[j]=t;}}
  for(int len=2;len<=m;len<<=1){Complex root=Complex.FromPolarCoordinates(1,-2*Math.PI/len);for(int i=0;i<m;i+=len){Complex w=Complex.One;for(int j=0;j<len/2;j++){Complex u=a[i+j],v=a[i+j+len/2]*w;a[i+j]=u+v;a[i+j+len/2]=u-v;w*=root;}}}
  double peak=0;for(int i=0;i<=m/2;i++)peak=Math.Max(peak,a[i].Magnitude);var db=new double[m/2+1];
  for(int i=0;i<db.Length;i++)db[i]=peak==0?-160:Math.Max(-160,20*Math.Log10(Math.Max(a[i].Magnitude/peak,1e-8)));return db;
 }
}
}
