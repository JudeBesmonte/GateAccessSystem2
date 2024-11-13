using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MySql.Data.MySqlClient;
using GateAccessSystem2.DB_Management;

namespace GateAccessSystem2
{
    public partial class LogIn : Form
    {
        public LogIn()
        {
            InitializeComponent();
        }
        private void tb_User_TextChanged(object sender, EventArgs e)
        {
            USER_placeholderLabel.Visible = false;
        }

        private void tb_Password_TextChanged(object sender, EventArgs e)
        {
            PW_placeholderlabel.Visible = false;
        }

        private void btn_Login_Click(object sender, EventArgs e)
        {
            string username = tb_User.Text; 
            string password = tb_Password.Text;

            // Check for empty fields
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) 
            { 
                MessageBox.Show("Please enter both username and password."); 
                return; 
            }
            if (ValidateUser(username, password))
            {
                MessageBox.Show("Login successful!");
                this.Hide();
                Form1 mainForm = new Form1(); // Create an instance of Form1
                mainForm.Show(); // Show Form
            }
            else 
            { 
                MessageBox.Show("Invalid username or password."); }
        }
        
        private bool ValidateUser(string username, string password) 
        { 
            bool isValid = false; 
            string query = "SELECT password_hash FROM login WHERE userID = @userID"; 
            
            using (var connection = Connection.GetConnection()) 
            { 
                using (var command = new MySqlCommand(query, connection)) 
                { 
                    command.Parameters.AddWithValue("@userID", username); 
                    
                    try 
                    { 
                        connection.Open(); 
                        string storedHash = command.ExecuteScalar()?.ToString(); 

                        if (storedHash != null && BCrypt.Net.BCrypt.Verify(password, storedHash)) 
                        { 
                            isValid = true; 
                        } 
                    } 
                    catch (Exception ex) 
                    { 
                        MessageBox.Show("An error occurred: " + ex.Message); 
                    } 
                } 
            } return isValid; }
        
    }    
}
