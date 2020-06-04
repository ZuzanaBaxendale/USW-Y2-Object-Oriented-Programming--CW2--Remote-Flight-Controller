using System;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Web;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace remoteFlightController
{
    //A struct that holds all the data to be transmitted
   //to the flight simulator
    public struct ControlsUpdate
    {
        public double elevatorPitch;
        public double throttle;
    }
    //A struct tht holds all the variables that are being passed from the 
    //flight simulator to the flight controller via the TCP connection
    public struct TelementryUpdate
    {
        public double speed;
        public double pitch;
        public double altitude;
        public double verticalSpeed;
        public double throttle;
        public double elevatorPitch;
        public int warningCode;
    }

    //delegate to handle recieving data is created and uses the object of the
    //telementry update struct
    //delegate to handle sending data is created and uses the object of the
    //controls update struct
    public delegate void RecievedDataHandler(TelementryUpdate telementryUpdate);
    public delegate void SendingDataHandler(ControlsUpdate controlsUpdate);

    public partial class frmFlightController : Form
    {
        //All classes and the TCP client are declared up
        //here so that they can be used to reference
        //the classes later on in my program
        DataReciever reciever = new DataReciever();
        DataSender sender = new DataSender();
        TcpClient client = new TcpClient();
        public frmFlightController()
        {
            //Components on GUI are initialised
            //events for sending and recieving are set up using the delegates created
            //above
            InitializeComponent();
            sender.Sending += new SendingDataHandler(setControlUpdates);
            reciever.Recieving += new RecievedDataHandler(getControlUpdates);
        }

        public void controlsScrollUpdate()
        {
            //created a network stream object using client's .GetStream function
            //Instance of controls update struct created
            NetworkStream stream = client.GetStream();
            ControlsUpdate controlsUpdate = new ControlsUpdate();
            //Sets the structs objects values to the current values from the sliders
            controlsUpdate.throttle = trkThrottle.Value;
            controlsUpdate.elevatorPitch = trkPitch.Value * 0.1;
            //stream uses the send data fucntion to send data
            //to be sent to the other program
            sender.stream = stream;
            sender.sendData(controlsUpdate);
            //thread sleeps for 300 milliseconds
            Thread.Sleep(0300);
        }

        private void trkThrottle_Scroll(object sender, EventArgs e)
        {
            //Label is updated to hold the value on the throttle slider
            //Constols scroll update method is called
            lblCurrentThrottle.Text = trkThrottle.Value.ToString() + ".0%";
            controlsScrollUpdate();
        }

        private void trkPitch_Scroll(object sender, EventArgs e)
        {
            //correct pitch value is calculated using tick frequency
            //Label is updated to hold the value on the pitch slider
            //Constols scroll update method is called
            double elevatorPitchV = (trkPitch.Value * 0.1) / trkPitch.TickFrequency;
            lblCurrentElevatorPitch.Text = elevatorPitchV.ToString() + "%";
            controlsScrollUpdate();
        }


        public class DataSender
        {
            //create event to handle sending data
            //create network stream object
            public event SendingDataHandler Sending;
            public NetworkStream stream;

            public void sendData(ControlsUpdate updateControls)
            {
                //string to hold data after being serialized is created
                //it is encoded to ascci bytes and put into the byte array
                //the data is then written to the stream and
                //the sending event is invoked
                JavaScriptSerializer serialise = new JavaScriptSerializer();
                string serializedData = serialise.Serialize(updateControls);
                byte[] data = Encoding.ASCII.GetBytes(serializedData);
                stream.Write(data, 0, data.Length);
                Sending?.Invoke(updateControls);
            }
        }
        public class DataReciever
        {
            //creates event to handle recieving data.
            public event RecievedDataHandler Recieving;
            //creates network stream object is made.
            public static NetworkStream stream;
            //Data has not started to be recieved yet.
            private bool startRetrievingData = false;

            //The method called to retrieve data.
            public void retrieveData()
            {
                //Telementary update declared.
                TelementryUpdate telementryUpdate = new TelementryUpdate();
                //Data is starting to be recieved.
                startRetrievingData = true;

                //Loop until recieving data is true.
                while (startRetrievingData)
                {
                    //create a character buffer.
                    //find the number of bites being read.
                    byte[] buffer = new byte[256];
                    int num_bytes = stream.Read(buffer, 0, 256);
                    //Get the ASCII data into a string variable.
                    string ToBeDeSerialized = Encoding.ASCII.GetString(buffer, 0, num_bytes);
                    //create a new serializer.
                    JavaScriptSerializer serialize = new JavaScriptSerializer();
                    //set the telementry update object's variables the the deserialized data.
                    telementryUpdate = serialize.Deserialize<TelementryUpdate>(ToBeDeSerialized);
                    //Invoked the recieving event is the null contitional operator allows it (?)
                    Recieving?.Invoke(telementryUpdate);

                }
            }
        }

        private void getControlUpdates(TelementryUpdate telementryUpdate)
        {
            //Will need to be involked if it is required
            //This recalles the function
            if (this.InvokeRequired)
            {
                this.Invoke(new RecievedDataHandler(getControlUpdates), new object[] { telementryUpdate });
            }
            else
            {
                //Updates the text boxes describing the current conditions of the plane
                //Math.round is used to round up the doubles to a whole number to make it easier 
                //to enterperate.
                txtAltitude.Text = Math.Round(telementryUpdate.altitude).ToString() + " ft";
                txtAirspeed.Text = Math.Round(telementryUpdate.speed).ToString() + " Knts";
                txtVerticalSpeed.Text = Math.Round(telementryUpdate.verticalSpeed).ToString() + " Fpm";
                txtThrottle.Text = telementryUpdate.throttle.ToString() + "%";
                txtPitchAngle.Text = Math.Round(telementryUpdate.pitch).ToString() + "°";
                txtElevatorPitch.Text = telementryUpdate.elevatorPitch.ToString() + "°";
                //Updates the data grid's cellswith the same values
                DataGridViewRow row = (DataGridViewRow)dgvDataRecieved.Rows[0].Clone();
                row.Cells[0].Value = telementryUpdate.speed.ToString();
                row.Cells[1].Value = telementryUpdate.verticalSpeed.ToString();
                row.Cells[2].Value = telementryUpdate.pitch.ToString();
                row.Cells[3].Value = telementryUpdate.altitude.ToString();
                row.Cells[4].Value = telementryUpdate.throttle.ToString();
                row.Cells[5].Value = telementryUpdate.elevatorPitch.ToString();
                row.Cells[6].Value = telementryUpdate.warningCode.ToString();

                dgvDataRecieved.Rows.Insert(0, row);

                if (dgvDataRecieved.Rows.Count > 10)
                {
                    dgvDataRecieved.Rows.RemoveAt(9);
                }
                //Performs warning checks
                //depending on what kidn of warning code it is
                //different text warnings are given
                if (telementryUpdate.warningCode == 1)
                {
                    lblWarning.Text = "Warning: Too low terrain";
                }
                else if (telementryUpdate.warningCode == 2)
                {
                    lblWarning.Text = "Warning: Stall risk";
                }
                else
                {
                    lblWarning.Text = "No warning";
                }
            }
        }

        private void setControlUpdates(ControlsUpdate controlUpdate)
        {
            //Will need to be involked if it is required
            //This recalles the function
            if (this.InvokeRequired)
            {

                this.Invoke(new SendingDataHandler(setControlUpdates), new object[] { controlUpdate }) ;
            }
            else
            {
                //updates the throttle and pitch values
                controlUpdate.throttle = trkThrottle.Value;
                controlUpdate.elevatorPitch = trkPitch.Value * 0.1;
            }
            
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            //Uppon connect button being pressed.
            //Takes in port and IP address from text box.
            int Port = int.Parse(txtPort.Text);
            IPAddress IP = IPAddress.Parse(txtIpAddress.Text);
            txtPort.Text = Convert.ToString(Port);
            //checks if the Ip address and port can be connected to
            //if so sliders are enabled and so is the connect button
            //Exception is thrown if connection is not possible.
            try
            {
                client.Connect(IP, Port);

                trkThrottle.Enabled = true;
                trkPitch.Enabled = true;
                btnConnect.Enabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            //New thread is created uppon start will perform the method
            //retrieveData
            Thread retriveThread = new Thread(new ThreadStart(reciever.retrieveData));
            retriveThread.Start();
            //A network stream object is established using the clients .GetStream method
            NetworkStream stream = client.GetStream();
            DataReciever.stream = stream;
            //Displays the current connection
            MessageBox.Show("Connected to: " + txtIpAddress.Text);
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            //Closes the form uppon the exit button being clicked.
            frmFlightController.ActiveForm.Close();
        }

        private void frmFlightController_Load(object sender, EventArgs e)
        {
            //Sets parameters uppon starting up the flight controller
            //sets min and maximum values for the pitch and throttle 
            //and disabled the pitch and throttle sliders until an event 
            //is triggured.
            trkPitch.Minimum = -50;
            trkPitch.Maximum = 50;
            trkThrottle.Minimum = 0;
            trkThrottle.Maximum = 100;
            trkThrottle.TickFrequency = 1;
            lblWarning.Text = "";
            trkPitch.Enabled = false;
            trkThrottle.Enabled = false;

        }
    }
}
