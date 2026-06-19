namespace BLTFRU_CLI
{
    internal class BltFruConfig
    {
        public string ManufacturerId  { get; set; }
        public string AssemblyNo      { get; set; }
        public string BoardId         { get; set; }
        public string HwRevision      { get; set; }
        public string FwRevision1     { get; set; }
        public string FwRevision2     { get; set; }
        public string FwRevision3     { get; set; }
        public string FwRevision4     { get; set; }
        public string FwRevision5     { get; set; }
        public string FwRevision6     { get; set; }
        public string CycleCounter    { get; set; }
        public string GlobalCounter   { get; set; }
        public byte   DeviceAddress   { get; set; }
        public byte   AddressingMode  { get; set; }
        public byte   PageSize        { get; set; }
    }
}
