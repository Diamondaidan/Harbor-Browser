using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace HarborBrowser.Updates
{
    public sealed class UpdateEnvelope { public string KeyId {get;set;} public string Payload {get;set;} public string Signature {get;set;} }
    public sealed class UpdatePackage
    {
        public string Version {get;set;} public string Url {get;set;} public string Sha256 {get;set;}
        public long Bytes {get;set;} public int DataSchema {get;set;}
        public string CertificateSha256 {get;set;}
    }
    public sealed class UpdateManifest
    {
        public int Schema {get;set;} public string Product {get;set;} public string Channel {get;set;}
        public string Architecture {get;set;} public long Sequence {get;set;}
        public string IssuedUtc {get;set;} public string ExpiresUtc {get;set;}
        public string Notes {get;set;} public UpdatePackage Latest {get;set;} public UpdatePackage Previous {get;set;}
        public string RollbackFrom {get;set;}
    }
    public sealed class VerifiedUpdate
    {
        public UpdateManifest Manifest {get;internal set;} public string Envelope {get;internal set;}
        public string PayloadHash {get;internal set;} public DateTime ExpiresUtc {get;internal set;}
    }
    public sealed class UpdateTrust
    {
        public string Owner {get;private set;} public string Repository {get;private set;}
        public string KeyId {get;private set;} public string PublicKey {get;private set;}
        public UpdateTrust(string owner,string repository,string keyId,string publicKey)
        {
            if(!Regex.IsMatch(owner??"","^[A-Za-z0-9-]{1,100}$")||!Regex.IsMatch(repository??"","^[A-Za-z0-9_.-]{1,100}$"))throw new ArgumentException("Invalid release repository.");
            Owner=owner;Repository=repository;KeyId=keyId;PublicKey=publicKey;
        }
        public Uri Feed {get{return new Uri("https://github.com/"+Owner+"/"+Repository+"/releases/latest/download/harbor-update.json");}}
        public string PackageUrl(string version){return "https://github.com/"+Owner+"/"+Repository+"/releases/download/v"+version+"/HarborBrowserSetup-"+version+"-release.exe";}
        public VerifiedUpdate Verify(string envelope,DateTime now)
        {
            if(envelope==null||Encoding.UTF8.GetByteCount(envelope)>131072)throw new InvalidDataException("The update record is too large.");
            var json=new JavaScriptSerializer{MaxJsonLength=131072,RecursionLimit=12};
            UpdateEnvelope outer;byte[] bytes,signature;
            try{outer=json.Deserialize<UpdateEnvelope>(envelope);if(outer==null||outer.KeyId!=KeyId)throw new InvalidDataException();bytes=Convert.FromBase64String(outer.Payload);signature=Convert.FromBase64String(outer.Signature);}
            catch{throw new InvalidDataException("The release signature record is invalid.");}
            if(bytes.Length>65536)throw new InvalidDataException("The signed update record is too large.");
            using(var rsa=new RSACryptoServiceProvider()){rsa.PersistKeyInCsp=false;rsa.FromXmlString(PublicKey);if(!rsa.VerifyData(bytes,CryptoConfig.MapNameToOID("SHA256"),signature))throw new InvalidDataException("The Harbor release signature did not match.");}
            UpdateManifest value;
            try{value=json.Deserialize<UpdateManifest>(new UTF8Encoding(false,true).GetString(bytes));}catch{throw new InvalidDataException("The signed release details are invalid.");}
            if(value==null||value.Schema!=1||value.Product!="HarborBrowser"||value.Channel!="stable"||value.Architecture!="x64"||value.Sequence<1||value.Notes==null||value.Notes.Length>12000)throw new InvalidDataException("This release does not match Harbor for Windows x64.");
            DateTime issued,expires;
            if(!DateTime.TryParseExact(value.IssuedUtc,"o",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind,out issued)||!DateTime.TryParseExact(value.ExpiresUtc,"o",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind,out expires)||issued.Kind!=DateTimeKind.Utc||expires.Kind!=DateTimeKind.Utc||issued>now.AddMinutes(5)||expires<=now||expires<=issued||expires-issued>TimeSpan.FromDays(31))throw new InvalidDataException("The release record expired or its dates are invalid. Check the PC clock and try again.");
            ValidatePackage(value.Latest);
            if(value.Previous!=null){ValidatePackage(value.Previous);if(VersionOf(value.Previous.Version)>=VersionOf(value.Latest.Version)||value.RollbackFrom!=value.Latest.Version||value.Previous.DataSchema!=value.Latest.DataSchema)throw new InvalidDataException("The previous release is not a compatible recovery target.");}
            return new VerifiedUpdate{Manifest=value,Envelope=envelope,PayloadHash=Hash(bytes),ExpiresUtc=expires};
        }
        private void ValidatePackage(UpdatePackage p)
        {
            if(p==null)throw new InvalidDataException("The release package is missing.");VersionOf(p.Version);
            if(p.Url!=PackageUrl(p.Version)||!Regex.IsMatch(p.Sha256??"","^[A-Fa-f0-9]{64}$")||p.Bytes<1024||p.Bytes>1073741824||p.DataSchema!=9||(!string.IsNullOrEmpty(p.CertificateSha256)&&!Regex.IsMatch(p.CertificateSha256,"^[A-Fa-f0-9]{64}$")))throw new InvalidDataException("The release package details are invalid.");
        }
        public static Version VersionOf(string text){Version value;if(!Regex.IsMatch(text??"","^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)$")||!Version.TryParse(text,out value))throw new InvalidDataException("Invalid Harbor version.");return value;}
        public static string Hash(byte[] bytes){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","");}
        public static string Hash(Stream stream){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","");}
        public static string HashFile(string path){using(var stream=File.OpenRead(path))return Hash(stream);}
    }
}
