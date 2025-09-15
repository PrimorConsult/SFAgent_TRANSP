using System.ComponentModel;
using System.ServiceProcess;

namespace SFAgent
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        private ServiceProcessInstaller _processInstaller;
        private ServiceInstaller _serviceInstaller;

        public ProjectInstaller()
        {
            _processInstaller = new ServiceProcessInstaller();
            _serviceInstaller = new ServiceInstaller();

            _processInstaller.Account = ServiceAccount.LocalSystem;

            _serviceInstaller.ServiceName = "SFAgent - Transportadora";
            _serviceInstaller.DisplayName = "SFAgent - Transportadora";
            _serviceInstaller.StartType = ServiceStartMode.Automatic;
            _serviceInstaller.Description = "Serviço que integra Transportadora do SAP HANA (OCRD) com Salesforce";

            Installers.Add(_processInstaller);
            Installers.Add(_serviceInstaller);
        }
    }
}