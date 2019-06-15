using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace VuduUHD.Web.Controllers
{
    [Route( "api/[controller]" )]
    public class SampleDataController : Controller
    {
        [HttpGet( "[action]" )]
        public async Task<IEnumerable<VuduResult>> VuduResults()
        {
            var allUhdResults = new List<VuduResult>();
            var dolbyVisionResults = new ConcurrentBag<VuduResult>();
            var offset = 0;

            for( ; offset < 700; offset += 100 )
            {
                try
                {
                    var url = $"https://apicache.vudu.com/api2/?_type=contentSearch&contentEncoding=gzip&count=100&dimensionality=any&followup=seasonNumber&format=application/json&offset=" + offset + "&responseSubset=micro&sortBy=-releaseTime&type=program&type=season&type=series&videoQuality=uhd";
                    var gzipMessageHandler = new HttpClientHandler
                                             {
                                                 AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
                                             };
                    var gzipClient = new GzipTestClient( new HttpClient( gzipMessageHandler ) );
                    var response = gzipClient.Get( url ).Result;

                    var stringResponse = String.Empty;
                    if( response.IsSuccessStatusCode )
                    {
                        stringResponse = await response.Content.ReadAsStringAsync();
                    }

                    stringResponse = stringResponse.Replace( "/*-secure-", "" );
                    stringResponse = stringResponse.Replace( "*/", "" );

                    using var document = JsonDocument.Parse( stringResponse );

                    var root = document.RootElement;
                    var contents = root.GetProperty( "content" );

                    foreach( var content in contents.EnumerateArray() )
                    {
                        var title = content.GetProperty( "title" ).EnumerateArray().First().GetString();
                        var contentId = content.GetProperty( "contentId" ).EnumerateArray().First().GetString();
                        allUhdResults.Add( new VuduResult
                                     {
                                         Title = title,
                                         ContentId = contentId
                                     } );
                    }
                }
                catch( Exception e )
                {

                    Console.WriteLine( e );
                }
            }

            var exceptions = new ConcurrentQueue<Exception>();

            Parallel.ForEach( allUhdResults, vr =>
                                             {
                                                 try
                                                 {
                                                     var url = "https://apicache.vudu.com/api2/?_type=contentSearch&contentEncoding=gzip&contentId=" + vr.ContentId + "&dimensionality=any&followup=ultraVioletability&followup=usefulStreamableOffers&followup=superType&followup=seasonNumber&followup=editions&format=application%2Fjson";

                                                     var gzipMessageHandler = new HttpClientHandler
                                                                              {
                                                                                  AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
                                                                              };
                                                     var gzipClient = new GzipTestClient( new HttpClient( gzipMessageHandler ) );
                                                     var response = gzipClient.Get( url ).Result;

                                                     var stringResponse = String.Empty;
                                                     if( response.IsSuccessStatusCode )
                                                     {
                                                         stringResponse = response.Content.ReadAsStringAsync().Result;
                                                     }

                                                     stringResponse = stringResponse.Replace( "/*-secure-", "" );
                                                     stringResponse = stringResponse.Replace( "*/", "" );

                                                     using var document = JsonDocument.Parse( stringResponse );

                                                     var root = document.RootElement;
                                                     var contents = root.GetProperty( "content" );

                                                     foreach( var content in contents.EnumerateArray() )
                                                     {
                                                         var release = content.GetProperty( "releaseTime" ).EnumerateArray().First().GetString();
                                                         var type = content.GetProperty( "type" ).EnumerateArray().First().GetString();
                                                         var superType = content.GetProperty( "superType" ).EnumerateArray().First().GetString();
                                                         var posterUrl = content.GetProperty( "posterUrl" ).EnumerateArray().First().GetString();
                                                         var description = content.GetProperty( "description" ).EnumerateArray().First().GetString();

                                                         foreach( var uhdEdition in content.GetProperty( "contentVariants" )
                                                                                           .EnumerateArray()
                                                                                           .First()
                                                                                           .GetProperty( "contentVariant" )
                                                                                           .EnumerateArray()
                                                                                           .Where( v => v.GetProperty( "videoQuality" ).EnumerateArray().First().GetString() == "uhd" )
                                                                                           .SelectMany( v => v.GetProperty( "editions" ).EnumerateArray().First().GetProperty( "edition" ).EnumerateArray() ) )
                                                         {
                                                             if( uhdEdition.GetProperty( "dynamicRange" ).EnumerateArray().First().GetString() != "dolbyVision" ) continue;

                                                             var dynamicRange = uhdEdition.GetProperty( "dynamicRange" ).EnumerateArray().First().GetString();

                                                             vr.ReleaseDate = DateTime.Parse( release );
                                                             vr.SuperType = superType;
                                                             vr.Type = type;
                                                             vr.PosterUrl = posterUrl;
                                                             vr.Description = description;
                                                             vr.DynamicRange = dynamicRange;

                                                             dolbyVisionResults.Add( vr );
                                                             break;
                                                         }
                                                     }
                                                 }
                                                 catch( Exception e )
                                                 {
                                                     exceptions.Enqueue( e );
                                                 }
                                             } );

            foreach ( var ex in exceptions)
            {
                Console.WriteLine( ex.Message );
            }

            return dolbyVisionResults.OrderByDescending( r => r.ReleaseDate );
        }

        public class GzipTestClient
        {
            HttpClient HttpClient { get; }
            public GzipTestClient( HttpClient client )
            {
                HttpClient = client;
                HttpClient.BaseAddress = new Uri( "http://httpbin.org" );
                HttpClient.DefaultRequestHeaders.AcceptEncoding.Add( new StringWithQualityHeaderValue( "gzip" ) );
                HttpClient.DefaultRequestHeaders.AcceptEncoding.Add( new StringWithQualityHeaderValue( "deflate" ) );
            }
            public async Task<HttpResponseMessage> Get( string url )
            {
                var request = new HttpRequestMessage( HttpMethod.Get, url );
                request.Headers.Add( "Accept", "application/json" );
                request.Headers.Add( "Accept-Encoding", "gzip, deflate, br" );
                request.Headers.Add( "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:68.0) Gecko/20100101 Firefox/68.0" );
                request.Headers.Add( "Referer", "https://www.vudu.com/" );
                request.Headers.Add( "Origin", "https://www.vudu.com" );
                request.Headers.Add( "Host", "apicache.vudu.com" );

                return await HttpClient.SendAsync( request );
            }
        }

        public class VuduResult
        {
            public string Title { get; set; }
            public string ContentId { get; set; }
            public string SuperType { get; set; }
            public string Type { get; set; }
            public string PosterUrl { get; set; }
            public string Description { get; set; }
            public string DynamicRange { get; set; }
            public DateTime ReleaseDate { get; set; }
        }
       
    }
}
