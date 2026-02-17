namespace Chat2Report.Services.Chart
{
    public class SupportedChart
    {
        public string Type { get; set; }
        public string Library { get; set; }
        public string Usage { get; set; }
        public string Example { get; set; }
        public string Function { get; set; }

        //we need to get dynamicity so charts libs can be added 
        //easily
    }
}
