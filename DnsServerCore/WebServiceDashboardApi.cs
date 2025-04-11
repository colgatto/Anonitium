/*
Technitium DNS Server
Copyright (C) 2025  Shreyas Zare (shreyas@technitium.com)

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.

*/

using DnsServerCore.Auth;
using DnsServerCore.Dns;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using TechnitiumLibrary.Net.Dns;
using TechnitiumLibrary.Net.Dns.ResourceRecords;

namespace DnsServerCore
{
    public partial class DnsWebService
    {
        class WebServiceDashboardApi
        {
            #region variables

            readonly DnsWebService _dnsWebService;

            #endregion

            #region constructor

            public WebServiceDashboardApi(DnsWebService dnsWebService)
            {
                _dnsWebService = dnsWebService;
            }

            #endregion

            #region private

            private static void WriteChartDataSet(Utf8JsonWriter jsonWriter, string label, string backgroundColor, string borderColor, List<KeyValuePair<string, long>> statsPerInterval)
            {
                jsonWriter.WriteStartObject();

                jsonWriter.WriteString("label", label);
                jsonWriter.WriteString("backgroundColor", backgroundColor);
                jsonWriter.WriteString("borderColor", borderColor);
                jsonWriter.WriteNumber("borderWidth", 2);
                jsonWriter.WriteBoolean("fill", true);

                jsonWriter.WritePropertyName("data");
                jsonWriter.WriteStartArray();

                foreach (KeyValuePair<string, long> item in statsPerInterval)
                    jsonWriter.WriteNumberValue(item.Value);

                jsonWriter.WriteEndArray();

                jsonWriter.WriteEndObject();
            }

            #endregion

            #region public

            public async Task GetStats(HttpContext context)
            {
                if (!_dnsWebService._authManager.IsPermitted(PermissionSection.Dashboard, context.GetCurrentSession().User, PermissionFlag.View))
                    throw new DnsWebServiceException("Access was denied.");

                HttpRequest request = context.Request;

                string strType = request.GetQueryOrForm("type", "lastHour");
                bool utcFormat = request.GetQueryOrForm("utc", bool.Parse, false);

                bool isLanguageEnUs = true;
                string acceptLanguage = request.Headers.AcceptLanguage;
                if (!string.IsNullOrEmpty(acceptLanguage))
                    isLanguageEnUs = acceptLanguage.StartsWith("en-us", StringComparison.OrdinalIgnoreCase);

                Dictionary<string, List<KeyValuePair<string, long>>> data;
                string labelFormat;

                switch (strType.ToLowerInvariant())
                {
                    case "lasthour":
                        data = _dnsWebService._dnsServer.StatsManager.GetLastHourMinuteWiseStats(utcFormat);
                        labelFormat = "HH:mm";
                        break;

                    case "lastday":
                        data = _dnsWebService._dnsServer.StatsManager.GetLastDayHourWiseStats(utcFormat);

                        if (isLanguageEnUs)
                            labelFormat = "MM/DD HH:00";
                        else
                            labelFormat = "DD/MM HH:00";

                        break;

                    case "lastweek":
                        data = _dnsWebService._dnsServer.StatsManager.GetLastWeekDayWiseStats(utcFormat);

                        if (isLanguageEnUs)
                            labelFormat = "MM/DD";
                        else
                            labelFormat = "DD/MM";

                        break;

                    case "lastmonth":
                        data = _dnsWebService._dnsServer.StatsManager.GetLastMonthDayWiseStats(utcFormat);

                        if (isLanguageEnUs)
                            labelFormat = "MM/DD";
                        else
                            labelFormat = "DD/MM";

                        break;

                    case "lastyear":
                        labelFormat = "MM/YYYY";
                        data = _dnsWebService._dnsServer.StatsManager.GetLastYearMonthWiseStats(utcFormat);
                        break;

                    case "custom":
                        string strStartDate = request.GetQueryOrForm("start");
                        string strEndDate = request.GetQueryOrForm("end");

                        if (!DateTime.TryParse(strStartDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime startDate))
                            throw new DnsWebServiceException("Invalid start date format.");

                        if (!DateTime.TryParse(strEndDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime endDate))
                            throw new DnsWebServiceException("Invalid end date format.");

                        if (startDate > endDate)
                            throw new DnsWebServiceException("Start date must be less than or equal to end date.");

                        TimeSpan duration = endDate - startDate;

                        if ((Convert.ToInt32(duration.TotalDays) + 1) > 7)
                        {
                            data = _dnsWebService._dnsServer.StatsManager.GetDayWiseStats(startDate, endDate, utcFormat);

                            if (isLanguageEnUs)
                                labelFormat = "MM/DD";
                            else
                                labelFormat = "DD/MM";
                        }
                        else if ((Convert.ToInt32(duration.TotalHours) + 1) > 3)
                        {
                            data = _dnsWebService._dnsServer.StatsManager.GetHourWiseStats(startDate, endDate, utcFormat);

                            if (isLanguageEnUs)
                                labelFormat = "MM/DD HH:00";
                            else
                                labelFormat = "DD/MM HH:00";
                        }
                        else
                        {
                            data = _dnsWebService._dnsServer.StatsManager.GetMinuteWiseStats(startDate, endDate, utcFormat);

                            if (isLanguageEnUs)
                                labelFormat = "MM/DD HH:mm";
                            else
                                labelFormat = "DD/MM HH:mm";
                        }

                        break;

                    default:
                        throw new DnsWebServiceException("Unknown stats type requested: " + strType);
                }

                Utf8JsonWriter jsonWriter = context.GetCurrentJsonWriter();

                //stats
                {
                    List<KeyValuePair<string, long>> stats = data["stats"];

                    jsonWriter.WritePropertyName("stats");
                    jsonWriter.WriteStartObject();

                    foreach (KeyValuePair<string, long> item in stats)
                        jsonWriter.WriteNumber(item.Key, item.Value);

                    jsonWriter.WriteNumber("zones", _dnsWebService._dnsServer.AuthZoneManager.TotalZones);
                    jsonWriter.WriteNumber("cachedEntries", _dnsWebService._dnsServer.CacheZoneManager.TotalEntries);
                    jsonWriter.WriteNumber("allowedZones", _dnsWebService._dnsServer.AllowedZoneManager.TotalZonesAllowed);
                    jsonWriter.WriteNumber("blockedZones", _dnsWebService._dnsServer.BlockedZoneManager.TotalZonesBlocked);
                    jsonWriter.WriteNumber("allowListZones", _dnsWebService._dnsServer.BlockListZoneManager.TotalZonesAllowed);
                    jsonWriter.WriteNumber("blockListZones", _dnsWebService._dnsServer.BlockListZoneManager.TotalZonesBlocked);

                    jsonWriter.WriteEndObject();
                }

                //main chart
                {
                    jsonWriter.WritePropertyName("mainChartData");
                    jsonWriter.WriteStartObject();

                    //label format
                    {
                        jsonWriter.WriteString("labelFormat", labelFormat);
                    }

                    //label
                    {
                        List<KeyValuePair<string, long>> statsPerInterval = data["totalQueriesPerInterval"];

                        jsonWriter.WritePropertyName("labels");
                        jsonWriter.WriteStartArray();

                        foreach (KeyValuePair<string, long> item in statsPerInterval)
                            jsonWriter.WriteStringValue(item.Key);

                        jsonWriter.WriteEndArray();
                    }

                    //datasets
                    {
                        jsonWriter.WritePropertyName("datasets");
                        jsonWriter.WriteStartArray();

                        WriteChartDataSet(jsonWriter, "Total", "rgba(102, 153, 255, 0.1)", "rgb(102, 153, 255)", data["totalQueriesPerInterval"]);
                        WriteChartDataSet(jsonWriter, "No Error", "rgba(92, 184, 92, 0.1)", "rgb(92, 184, 92)", data["totalNoErrorPerInterval"]);
                        WriteChartDataSet(jsonWriter, "Server Failure", "rgba(217, 83, 79, 0.1)", "rgb(217, 83, 79)", data["totalServerFailurePerInterval"]);
                        WriteChartDataSet(jsonWriter, "NX Domain", "rgba(120, 120, 120, 0.1)", "rgb(120, 120, 120)", data["totalNxDomainPerInterval"]);
                        WriteChartDataSet(jsonWriter, "Refused", "rgba(91, 192, 222, 0.1)", "rgb(91, 192, 222)", data["totalRefusedPerInterval"]);

                        WriteChartDataSet(jsonWriter, "Authoritative", "rgba(150, 150, 0, 0.1)", "rgb(150, 150, 0)", data["totalAuthHitPerInterval"]);
                        WriteChartDataSet(jsonWriter, "Recursive", "rgba(23, 162, 184, 0.1)", "rgb(23, 162, 184)", data["totalRecursionsPerInterval"]);
                        WriteChartDataSet(jsonWriter, "Cached", "rgba(111, 84, 153, 0.1)", "rgb(111, 84, 153)", data["totalCacheHitPerInterval"]);
                        WriteChartDataSet(jsonWriter, "Blocked", "rgba(255, 165, 0, 0.1)", "rgb(255, 165, 0)", data["totalBlockedPerInterval"]);
                        WriteChartDataSet(jsonWriter, "Dropped", "rgba(30, 30, 30, 0.1)", "rgb(30, 30, 30)", data["totalDroppedPerInterval"]);

                        jsonWriter.WriteEndArray();
                    }

                    jsonWriter.WriteEndObject();
                }

                //query response chart
                {
                    jsonWriter.WritePropertyName("queryResponseChartData");
                    jsonWriter.WriteStartObject();

                    List<KeyValuePair<string, long>> stats = data["stats"];

                    //labels
                    {
                        jsonWriter.WritePropertyName("labels");
                        jsonWriter.WriteStartArray();

                        foreach (KeyValuePair<string, long> item in stats)
                        {
                            switch (item.Key)
                            {
                                case "totalAuthoritative":
                                    jsonWriter.WriteStringValue("Authoritative");
                                    break;

                                case "totalRecursive":
                                    jsonWriter.WriteStringValue("Recursive");
                                    break;

                                case "totalCached":
                                    jsonWriter.WriteStringValue("Cached");
                                    break;

                                case "totalBlocked":
                                    jsonWriter.WriteStringValue("Blocked");
                                    break;

                                case "totalDropped":
                                    jsonWriter.WriteStringValue("Dropped");
                                    break;
                            }
                        }

                        jsonWriter.WriteEndArray();
                    }

                    //datasets
                    {
                        jsonWriter.WritePropertyName("datasets");
                        jsonWriter.WriteStartArray();

                        jsonWriter.WriteStartObject();

                        jsonWriter.WritePropertyName("data");
                        jsonWriter.WriteStartArray();

                        foreach (KeyValuePair<string, long> item in stats)
                        {
                            switch (item.Key)
                            {
                                case "totalAuthoritative":
                                case "totalRecursive":
                                case "totalCached":
                                case "totalBlocked":
                                case "totalDropped":
                                    jsonWriter.WriteNumberValue(item.Value);
                                    break;
                            }
                        }

                        jsonWriter.WriteEndArray();

                        jsonWriter.WritePropertyName("backgroundColor");
                        jsonWriter.WriteStartArray();
                        jsonWriter.WriteStringValue("rgba(150, 150, 0, 0.5)");
                        jsonWriter.WriteStringValue("rgba(23, 162, 184, 0.5)");
                        jsonWriter.WriteStringValue("rgba(111, 84, 153, 0.5)");
                        jsonWriter.WriteStringValue("rgba(255, 165, 0, 0.5)");
                        jsonWriter.WriteStringValue("rgba(7, 7, 7, 0.5)");
                        jsonWriter.WriteEndArray();

                        jsonWriter.WriteEndObject();

                        jsonWriter.WriteEndArray();
                    }

                    jsonWriter.WriteEndObject();
                }

                //query type chart
                {
                    jsonWriter.WritePropertyName("queryTypeChartData");
                    jsonWriter.WriteStartObject();

                    List<KeyValuePair<string, long>> queryTypes = data["queryTypes"];

                    //labels
                    {
                        jsonWriter.WritePropertyName("labels");
                        jsonWriter.WriteStartArray();

                        foreach (KeyValuePair<string, long> item in queryTypes)
                            jsonWriter.WriteStringValue(item.Key);

                        jsonWriter.WriteEndArray();
                    }

                    //datasets
                    {
                        jsonWriter.WritePropertyName("datasets");
                        jsonWriter.WriteStartArray();

                        jsonWriter.WriteStartObject();

                        jsonWriter.WritePropertyName("data");
                        jsonWriter.WriteStartArray();

                        foreach (KeyValuePair<string, long> item in queryTypes)
                            jsonWriter.WriteNumberValue(item.Value);

                        jsonWriter.WriteEndArray();

                        jsonWriter.WritePropertyName("backgroundColor");
                        jsonWriter.WriteStartArray();
                        jsonWriter.WriteStringValue("rgba(102, 153, 255, 0.5)");
                        jsonWriter.WriteStringValue("rgba(92, 184, 92, 0.5)");
                        jsonWriter.WriteStringValue("rgba(7, 7, 7, 0.5)");
                        jsonWriter.WriteStringValue("rgba(91, 192, 222, 0.5)");
                        jsonWriter.WriteStringValue("rgba(150, 150, 0, 0.5)");
                        jsonWriter.WriteStringValue("rgba(23, 162, 184, 0.5)");
                        jsonWriter.WriteStringValue("rgba(111, 84, 153, 0.5)");
                        jsonWriter.WriteStringValue("rgba(255, 165, 0, 0.5)");
                        jsonWriter.WriteStringValue("rgba(51, 122, 183, 0.5)");
                        jsonWriter.WriteStringValue("rgba(150, 150, 150, 0.5)");
                        jsonWriter.WriteEndArray();

                        jsonWriter.WriteEndObject();

                        jsonWriter.WriteEndArray();
                    }

                    jsonWriter.WriteEndObject();
                }

                //protocol type chart
                {
                    jsonWriter.WritePropertyName("protocolTypeChartData");
                    jsonWriter.WriteStartObject();

                    List<KeyValuePair<string, long>> protocolTypes = data["protocolTypes"];

                    //labels
                    {
                        jsonWriter.WritePropertyName("labels");
                        jsonWriter.WriteStartArray();

                        foreach (KeyValuePair<string, long> item in protocolTypes)
                            jsonWriter.WriteStringValue(item.Key);

                        jsonWriter.WriteEndArray();
                    }

                    //datasets
                    {
                        jsonWriter.WritePropertyName("datasets");
                        jsonWriter.WriteStartArray();

                        jsonWriter.WriteStartObject();

                        jsonWriter.WritePropertyName("data");
                        jsonWriter.WriteStartArray();

                        foreach (KeyValuePair<string, long> item in protocolTypes)
                            jsonWriter.WriteNumberValue(item.Value);

                        jsonWriter.WriteEndArray();

                        jsonWriter.WritePropertyName("backgroundColor");
                        jsonWriter.WriteStartArray();
                        jsonWriter.WriteStringValue("rgba(111, 84, 153, 0.5)");
                        jsonWriter.WriteStringValue("rgba(150, 150, 0, 0.5)");
                        jsonWriter.WriteStringValue("rgba(23, 162, 184, 0.5)"); ;
                        jsonWriter.WriteStringValue("rgba(255, 165, 0, 0.5)");
                        jsonWriter.WriteStringValue("rgba(91, 192, 222, 0.5)");
                        jsonWriter.WriteEndArray();

                        jsonWriter.WriteEndObject();

                        jsonWriter.WriteEndArray();
                    }

                    jsonWriter.WriteEndObject();
                }
            }

            #endregion
        }
    }
}
