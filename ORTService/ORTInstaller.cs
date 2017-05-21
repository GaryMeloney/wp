using System.ComponentModel;

namespace ORTService
{
    /// <summary>
    /// Summary description for TCPInstaller.
    /// </summary>
    [RunInstaller(true)]
    public class ORTInstaller : System.Configuration.Install.Installer
    {
        private System.ServiceProcess.ServiceProcessInstaller ORTServiceProcessInstaller;
        private System.ServiceProcess.ServiceInstaller ORTServiceInstaller;
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.Container components = null;

        public ORTInstaller()
        {
            // This call is required by the Designer.
            InitializeComponent();

            // TODO: Add any initialization after the InitializeComponent call
        }

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }


        #region Component Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.ORTServiceProcessInstaller = new System.ServiceProcess.ServiceProcessInstaller();
            this.ORTServiceInstaller = new System.ServiceProcess.ServiceInstaller();
            // 
            // ORTServiceProcessInstaller
            // 
            this.ORTServiceProcessInstaller.Account = System.ServiceProcess.ServiceAccount.LocalSystem;
            this.ORTServiceProcessInstaller.Password = null;
            this.ORTServiceProcessInstaller.Username = null;
            // 
            // ORTServiceInstaller
            // 
            this.ORTServiceInstaller.DisplayName = "ORTService";
            this.ORTServiceInstaller.ServiceName = "ORTService";
            // 
            // TCPInstaller
            // 
            this.Installers.AddRange(new System.Configuration.Install.Installer[] { this.ORTServiceProcessInstaller, this.ORTServiceInstaller});

        }
        #endregion
    }
}
