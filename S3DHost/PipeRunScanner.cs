using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Ingr.SP3D.Common.Client;
using Ingr.SP3D.Common.Client.Services;
using Ingr.SP3D.Common.Middle;
using PaintRules;
using S3DAdapter;

namespace YYB
{
    // Called only on the S3D command thread. BusinessObjects must not be used by report workers.
    internal sealed class PipeRunScanner
    {
        private readonly HostOptions options;
        internal PipeRunScanner(HostOptions options) { this.options = options; }

        internal List<RunObject> ReadWorkingSet()
        {
            var filter = new Filter();
            filter.Definition.AddObjectType(@"Piping\PipingRuns");
            var objects = ClientServiceProvider.WorkingSet.GetObjectsByFilter(
                filter, ClientServiceProvider.WorkingSet.ActiveConnection);
            var runs = new List<RunObject>();
            foreach (BusinessObject obj in objects)
            {
                if (obj == null || !obj.SupportsInterface("IJRtePipeRun")) continue;
                var oid = obj.ObjectID;
                if (string.IsNullOrWhiteSpace(oid)) continue;
                var rawMax = Number(obj, "IJProcessDataInfo", "OperatingMaxTemp");
                var rawOperating = Number(obj, "IJProcessDataInfo", "OperatingTemp");
                var rawDesign = Number(obj, "IJProcessDataInfo", "DesignMaxTemp");
                var data = new PipeRunData(oid,
                    Text(obj, "IJNamedItem", "Name") ?? oid,
                    "", ReadSpec(obj),
                    TemperatureUnits.KelvinToCelsius(rawMax),
                    TemperatureUnits.KelvinToCelsius(rawOperating),
                    TemperatureUnits.KelvinToCelsius(rawDesign),
                    InsulationPurpose(obj),
                    InsulationThickness(obj),
                    ReadInsulationSpec(obj),
                    ReadUnderground(obj),
                    CodeName(obj, "IJCoatingInfo", "CoatingType"),
                    // The standard's key service conditions have no confirmed source fields.
                    new Dictionary<string, bool?> { ["KeyServiceCondition"] = null });
                runs.Add(new RunObject(obj, data));
            }
            return runs;
        }

        internal void WriteBatch(IReadOnlyList<RunObject> objects,
            IReadOnlyList<EvaluationResult> approved)
        {
            var byId = objects.ToDictionary(x => x.Data.Oid, StringComparer.Ordinal);
            var writes = new List<Action>();
            foreach (var result in approved)
            {
                if (result.Status != PaintStatus.AUTO_SET || string.IsNullOrWhiteSpace(result.ResultPaintCode))
                    throw new InvalidOperationException("Non-approved or empty Paint Code in write batch.");
                var obj = byId[result.Run.Oid].Object;
                if (!string.Equals(CodeName(obj, "IJCoatingInfo", "CoatingType"),
                    result.Run.ExistingPaintCode, StringComparison.Ordinal))
                    throw new InvalidOperationException("CoatingType changed since scan: " + result.Run.Oid);
                var value = obj.GetPropertyValue("IJCoatingInfo", "CoatingType") as PropertyValueCodelist;
                var info = value?.PropertyInfo?.CodeListInfo;
                if (info == null) throw new InvalidOperationException("CoatingType CodeList unavailable: " + result.Run.Oid);
                var item = info.GetCodelistItem(result.ResultPaintCode);
                if (item == null || !string.Equals(item.Name, result.ResultPaintCode, StringComparison.Ordinal))
                    throw new InvalidOperationException("Paint Code not in target Plant CodeList: " + result.ResultPaintCode);
                writes.Add(() => obj.SetPropertyValue(item, value!.PropertyInfo));
            }

            try
            {
                foreach (var write in writes) write();
                ClientServiceProvider.TransactionMgr.Compute();
                ClientServiceProvider.TransactionMgr.Commit("S3D Auto Paint - " + writes.Count + " PipeRuns");
            }
            catch
            {
                ClientServiceProvider.TransactionMgr.Abort();
                throw;
            }
        }

        private bool? ReadUnderground(BusinessObject obj)
        {
            if (string.IsNullOrWhiteSpace(options.UndergroundInterface) ||
                string.IsNullOrWhiteSpace(options.UndergroundProperty)) return null;
            var text = Text(obj, options.UndergroundInterface, options.UndergroundProperty);
            if (text == null) return null;
            if (bool.TryParse(text, out var flag)) return flag;
            if (text == "1") return true;
            if (text == "0") return false;
            return null;
        }

        private static string? ReadSpec(BusinessObject obj)
        {
            try
            {
                var related = obj.GetRelationship("PathRunUsesSpec", "Spec").TargetObjects;
                if (related.Count > 0)
                {
                    var spec = related[0];
                    return Text(spec, "IJDPipeSpec", "SpecName") ?? Text(spec, "IJNamedItem", "Name");
                }
            }
            catch { }
            return null;
        }

        private static string? ReadInsulationSpec(BusinessObject obj)
        {
            try
            {
                var property = obj.GetType().GetProperty("InsulationSpec", BindingFlags.Public | BindingFlags.Instance);
                var value = property?.GetValue(obj, null) as BusinessObject;
                return value == null ? null : Text(value, "IJNamedItem", "Name");
            }
            catch { return null; }
        }

        private static string InsulationPurpose(BusinessObject obj)
        {
            // Missing interface/property must not be mistaken for an exposed PipeRun.
            if (!obj.SupportsInterface("IJRteInsulation")) return "__UNREADABLE__";
            try
            {
                obj.GetPropertyValue("IJRteInsulation", "Purpose");
                return CodeName(obj, "IJRteInsulation", "Purpose") ?? "__UNREADABLE__";
            }
            catch { return "__UNREADABLE__"; }
        }

        private static decimal? InsulationThickness(BusinessObject obj)
        {
            var thickness = Number(obj, "IJRteInsulation", "Thickness");
            if (thickness.HasValue) return thickness;
            try
            {
                var property = obj.GetType().GetProperty("InsulationThickness", BindingFlags.Public | BindingFlags.Instance);
                var raw = property?.GetValue(obj, null);
                return raw == null ? null : Convert.ToDecimal(raw, CultureInfo.InvariantCulture);
            }
            catch { return null; }
        }

        private static decimal? Number(BusinessObject obj, string iface, string name)
        {
            var text = Text(obj, iface, name);
            return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : (decimal?)null;
        }

        private static string? CodeName(BusinessObject obj, string iface, string name)
        {
            try
            {
                var value = obj.GetPropertyValue(iface, name) as PropertyValueCodelist;
                if (value == null) return Text(obj, iface, name);
                var item = value.PropertyInfo?.CodeListInfo?.GetCodelistItem(value.PropValue);
                return item?.Name ?? Convert.ToString(value.PropValue, CultureInfo.InvariantCulture);
            }
            catch { return null; }
        }

        private static string? Text(BusinessObject obj, string iface, string name)
        {
            try
            {
                var value = obj.GetPropertyValue(iface, name);
                var prop = value.GetType().GetProperty("PropValue", BindingFlags.Public | BindingFlags.Instance);
                var raw = prop?.GetValue(value, null);
                return raw == null ? null : Convert.ToString(raw, CultureInfo.InvariantCulture);
            }
            catch { return null; }
        }
    }

    internal sealed class RunObject
    {
        internal RunObject(BusinessObject obj, PipeRunData data) { Object = obj; Data = data; }
        internal BusinessObject Object { get; }
        internal PipeRunData Data { get; }
    }
}
