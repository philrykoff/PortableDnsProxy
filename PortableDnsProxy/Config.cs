﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace PortableDnsProxy
{
    public partial class Config : Form
    {
        public Config()
        {
            InitializeComponent();
        }

        private void Config_Load(object sender, EventArgs e)
        {
            cbxProxyType.SelectedIndex = 0;

            ImportSettings();

            if (tbxPort.Text.Equals(String.Empty))
            {
                tbxPort.Text = "8042";
            }

            if(Utils.CertificatesExist())
            {
                btnClearTlsCertificateCache.Enabled = true;
            }
        }

        private void ImportSettings()
        {
            Utils.DbSettings settings = Utils.ReadSettings();

            if(settings != null)
            {
                if (settings.ProxyType == Utils.DbProxyType.SystemDefault)
                {
                    cbxProxyType.SelectedItem = "System default";
                    tbxProxyHost.Text = "";
                }
                else if (settings.ProxyType != Utils.DbProxyType.None)
                {
                    cbxProxyType.SelectedItem = settings.ProxyType.ToString().Replace("Http", "HTTP").Replace("Socks", "SOCKS");
                    tbxProxyHost.Text = settings.ProxyHost + ":" + settings.ProxyPort.ToString();
                }

                tbxPort.Text = settings.Port.ToString();

                foreach(Utils.DbHost host in settings.Hosts)
                {
                    dgvHosts.Rows.Add(host.OriginalName == null ? "" : host.OriginalName, 
                                      host.RedirectedNameOrIp == null ? "" : host.RedirectedNameOrIp, 
                                      host.RewrittenHostHeader == null ? "" : host.RewrittenHostHeader);
                }
            }
        }

        private bool ExportSettings()
        {
            Registry.CurrentUser.DeleteSubKeyTree("SOFTWARE\\PhilsIndustries\\PortableDnsProxy", false);
            RegistryKey settingKey = Registry.CurrentUser.CreateSubKey("SOFTWARE\\PhilsIndustries\\PortableDnsProxy");

            if(cbxProxyType.SelectedItem != null &&
               !cbxProxyType.SelectedItem.Equals("None"))
            {
                if(cbxProxyType.SelectedItem.Equals("System default"))
                {
                    settingKey.SetValue("proxy_type", "System default");
                }
                else if(!tbxProxyHost.Text.Trim().Equals(""))
                {
                    string proxyHost;
                    int proxyPort = 8080;

                    if (tbxProxyHost.Text.Contains(":"))
                    {
                        proxyHost = tbxProxyHost.Text.Substring(0, tbxProxyHost.Text.LastIndexOf(":"));
                        if (!int.TryParse(tbxProxyHost.Text.Substring(tbxProxyHost.Text.LastIndexOf(":") + 1), out proxyPort) || proxyPort < 1 || proxyPort > 65535)
                        {
                            Utils.ShowError("Please enter a valid port number [1 - 65535] for your HTTP proxy into the HTTP proxy textbox.");
                            return false;
                        }
                    }
                    else
                    {
                        proxyHost = tbxProxyHost.Text;
                    }

                    settingKey.SetValue("proxy_type", cbxProxyType.SelectedItem);
                    settingKey.SetValue("proxy_host", proxyHost);
                    settingKey.SetValue("proxy_port", proxyPort);
                }
            }

            int port;
            if (!int.TryParse(tbxPort.Text, out port) || port < 1 || port > 65535)
            {
                Utils.ShowError("Please enter a valid port number [1 - 65535] into the port textbox.");
                return false;
            }
            settingKey.SetValue("port", port);

            if (dgvHosts.Rows.Count > 0)
            {
                RegistryKey hostsKey = settingKey.CreateSubKey("Hosts");

                foreach (DataGridViewRow row in dgvHosts.Rows)
                {
                    if (row.Cells[0].Value.ToString().Equals(""))
                    {
                        continue;
                    }

                    RegistryKey hostKey = hostsKey.CreateSubKey(row.Cells[0].Value.ToString());

                    hostKey.SetValue("host_redirect", row.Cells[1].Value.ToString());
                    hostKey.SetValue("host_header_overwrite", row.Cells[2].Value.ToString());
                }
            }

            return true;
        }

        private void btnAddHost_Click(object sender, EventArgs e)
        {
            using (ConfigHost frmConfigHost = new ConfigHost())
            {
                if (DialogResult.OK == frmConfigHost.ShowDialog(this))
                {
                    // If another record with the changed hostname already exists, overwrite that
                    bool overwritten = false;
                    foreach (DataGridViewRow row in dgvHosts.Rows)
                    {
                        if (row.Cells[0].Value.ToString().Equals(frmConfigHost.tbxHostMatch.Text))
                        {
                            row.Cells[1].Value = frmConfigHost.tbxHostRedirect.Text;
                            row.Cells[2].Value = frmConfigHost.tbxHostHeaderOverwrite.Text;
                            overwritten = true;
                            break;
                        }
                    }

                    if (!overwritten)
                    {
                        dgvHosts.Rows.Add(frmConfigHost.tbxHostMatch.Text, frmConfigHost.tbxHostRedirect.Text, frmConfigHost.tbxHostHeaderOverwrite.Text);
                    }
                }
            }
        }

        private void dgvHosts_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && dgvHosts.Rows[e.RowIndex] != null)
            {
                DataGridViewRow editedRow = dgvHosts.Rows[e.RowIndex];

                using (ConfigHost frmConfigHost = new ConfigHost(editedRow.Cells[0].Value.ToString(),
                                                                 editedRow.Cells[1].Value.ToString(),
                                                                 editedRow.Cells[2].Value.ToString()))
                {
                    if (DialogResult.OK == frmConfigHost.ShowDialog(this))
                    {
                        editedRow.Cells[0].Value = frmConfigHost.tbxHostMatch.Text;
                        editedRow.Cells[1].Value = frmConfigHost.tbxHostRedirect.Text;
                        editedRow.Cells[2].Value =  frmConfigHost.tbxHostHeaderOverwrite.Text;

                        // If another record with the changed hostname already exists, overwrite that
                        foreach (DataGridViewRow row in dgvHosts.Rows)
                        {
                            if (row.Index != e.RowIndex && row.Cells[0].Value.ToString().Equals(frmConfigHost.tbxHostMatch.Text))
                            {
                                dgvHosts.Rows.RemoveAt(row.Index);
                                break;
                            }
                        }
                    }
                }
            }
        }

        private void btnRemoveHost_Click(object sender, EventArgs e)
        {
            if(dgvHosts.SelectedRows != null && dgvHosts.SelectedCells.Count > 0)
            {
                dgvHosts.Rows.Remove(dgvHosts.SelectedCells[0].OwningRow);
            }
        }

        private void btnClearTlsCertificateCache_Click(object sender, EventArgs e)
        {
            if(DialogResult.Yes == MessageBox.Show("If you clear your TLS certificate cache, you probably get these nasty warnings in your browser regarding insecure certificates again.\n\nAre you sure you would want this?!",
                                                   "Attention ihr Menschen!",
                                                   MessageBoxButtons.YesNo,
                                                   MessageBoxIcon.Hand,
                                                   MessageBoxDefaultButton.Button2))
            {
                Utils.ClearCertificates();

                if (Utils.CertificatesExist())
                {
                    Utils.ShowError("The certificate cache could not be cleared.");
                }
                else
                {
                    Utils.ShowInfo("Your certificate cache has successfully been cleared.");
                    btnClearTlsCertificateCache.Enabled = false;
                }
            }
        }

        private void btnSaveAndClose_Click(object sender, EventArgs e)
        {
            if(ExportSettings())
            {
                Close();
            }
        }

        private void cbxProxyType_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbxProxyType.SelectedItem == null || cbxProxyType.SelectedItem.Equals("None") || cbxProxyType.SelectedItem.Equals("System default"))
            {
                tbxProxyHost.Enabled = false;
                tbxProxyHost.Text = "";
            }
            else
            {
                tbxProxyHost.Enabled = true;
                tbxProxyHost.Focus();
            }
        }
    }
}
