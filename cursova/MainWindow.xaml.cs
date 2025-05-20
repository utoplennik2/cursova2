using System;
using System.Collections.Generic;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MySql.Data.MySqlClient;

namespace cursova
{
    public partial class MainWindow : Window
    {
        private int currentUserId = -1; // Номер користувача який не авторизован
        private List<(TimeSpan Start, TimeSpan End)> WorkingHours = new List<(TimeSpan, TimeSpan)>
        {
            (new TimeSpan(8, 0, 0), new TimeSpan(9, 30, 0)),                          //годтни прийому
            (new TimeSpan(10, 0, 0), new TimeSpan(11, 30, 0)),
            (new TimeSpan(12, 0, 0), new TimeSpan(13, 30, 0)),
            (new TimeSpan(14, 0, 0), new TimeSpan(15, 30, 0)),
            (new TimeSpan(16, 0, 0), new TimeSpan(17, 30, 0)),
            (new TimeSpan(18, 0, 0), new TimeSpan(19, 30, 0))
        };

        private void LoginButton_Click(object sender, RoutedEventArgs e)             //лоігнування користувача
        {
            string username = LoginUsernameTextBox.Text;
            string password = LoginPasswordBox.Password;

            try
            {
                using (MySqlConnection conn = DBMySQLUtils.GetDBConnection())
                {
                    conn.Open();
                    string query = "SELECT id_user FROM users WHERE username = @username AND password = @password AND status = 'active'";
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@username", username);
                    cmd.Parameters.AddWithValue("@password", password);
                    object result = cmd.ExecuteScalar();
                    if (result != null)
                    {
                        currentUserId = Convert.ToInt32(result);
                        LoginMessage.Text = "Успішний вхід!";
                        LoadPsychologists();
                        LoadHistory();
                        LoadAppointmentsForFeedback();
                        LoadFeedbackHistory();
                        LoadPsychologistsList();
                    }
                    else
                    {
                        LoginMessage.Text = "Неправильний логін або пароль!";
                    }
                }
            }
            catch (Exception ex)
            {
                LoginMessage.Text = "Не вдалося під'єднатися до бази даних: " + ex.Message;
            }
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)               //реєстрація
        {
            string username = RegisterUsernameTextBox.Text;
            string password = RegisterPasswordBox.Password;
            string fullName = RegisterFullNameTextBox.Text;
            string phone = RegisterPhoneTextBox.Text;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(fullName))
            {
                RegisterMessage.Text = "Заповни ім'я, пароль і повне ім'я!";
                return;
            }

            if (!string.IsNullOrEmpty(phone) && !phone.StartsWith("+"))
            {
                RegisterMessage.Text = "Номер телефону має починатися з '+'!";
                return;
            }

            try
            {
                using (MySqlConnection conn = DBMySQLUtils.GetDBConnection())
                {
                    conn.Open();
                    string checkQuery = "SELECT COUNT(*) FROM users WHERE username = @username"; //скільки у табл має нік
                    MySqlCommand checkCmd = new MySqlCommand(checkQuery, conn);
                    checkCmd.Parameters.AddWithValue("@username", username);
                    long userCount = (long)checkCmd.ExecuteScalar();
                    if (userCount > 0)
                    {
                        RegisterMessage.Text = "Такий користувач уже є!";
                        return;
                    }

                    string insertQuery = "INSERT INTO users (username, password, full_name, phone_number, status, registration_date) VALUES (@username, @password, @full_name, @phone_number, 'active', NOW())";
                    MySqlCommand insertCmd = new MySqlCommand(insertQuery, conn);
                    insertCmd.Parameters.AddWithValue("@username", username);
                    insertCmd.Parameters.AddWithValue("@password", password);
                    insertCmd.Parameters.AddWithValue("@full_name", fullName);
                    insertCmd.Parameters.AddWithValue("@phone_number", phone ?? "");  //якщо нулл порожнє значення, якщо не пусте то його 
                    insertCmd.ExecuteNonQuery();

                    string idQuery = "SELECT LAST_INSERT_ID()";
                    MySqlCommand idCmd = new MySqlCommand(idQuery, conn);
                    currentUserId = Convert.ToInt32(idCmd.ExecuteScalar());        // стоврення нового айді в бд

                    RegisterMessage.Text = "Вітаю! Ви зареєстровані";
                    LoadPsychologists();
                    LoadHistory();
                    LoadAppointmentsForFeedback();
                    LoadFeedbackHistory();
                    LoadPsychologistsList();
                }
            }
            catch (Exception ex)
            {
                RegisterMessage.Text = "Не вдалося під'єднатися до бази даних: " + ex.Message;
            }
        }

        private void LoadPsychologists()                          // 3 вкладка бронювання психолоогів
        {
            if (currentUserId == -1) return;  //якщо корист не авторизований не побачимо дані у вікні

            PsychologistComboBox.Items.Clear();  //щоб не дублювалося
            try
            {
                using (MySqlConnection conn = DBMySQLUtils.GetDBConnection())
                {
                    conn.Open();
                    string query = "SELECT psychologist_id, full_name, availability_status FROM psychologists";
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    MySqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var id = reader.GetInt32("psychologist_id");
                        var name = reader.GetString("full_name");
                        var isAvailable = reader.GetString("availability_status") == "available";

                        if (isAvailable)
                        {
                            PsychologistComboBox.Items.Add(new KeyValuePair<int, string>(id, name));
                        }
                        else
                        {
                            PsychologistComboBox.Items.Add(new ComboBoxItem
                            {
                                Content = $"{name} (недоступний/а)",
                                IsEnabled = false
                            });
                        }
                    }
                    reader.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Помилка завантаження психологів: " + ex.Message);
            }
        }

        private void LoadPsychologistsList()                      //2 вкладка УСІ психологи 
        {
            PsychologistsListBox.Items.Clear();
            try
            {
                using (MySqlConnection conn = DBMySQLUtils.GetDBConnection())
                {
                    conn.Open();
                    string query = "SELECT psychologist_id, full_name, bio, email, qualifications, phone_number, registration_date, availability_status FROM psychologists";
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    MySqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        string availability = reader.GetString("availability_status");
                        string color = availability == "available" ? "Green" : "Red";    //якщо евейлбл зел, інакше червон            
                        PsychologistsListBox.Items.Add(new
                        {
                            psychologist_id = reader.GetInt32("psychologist_id"),
                            full_name = reader.GetString("full_name"),
                            bio = reader.IsDBNull("bio") ? "Немає біо" : reader.GetString("bio"),//якщо нул, немає, є читаємо
                            email = reader.GetString("email"),
                            qualifications = reader.GetString("qualifications"),
                            phone_number = reader.IsDBNull("phone_number") ? "Немає телефону" : reader.GetString("phone_number"),
                            registration_date = reader.GetDateTime("registration_date").ToString("yyyy-MM-dd HH:mm:ss"),
                            availability_status = availability == "available" ? "Доступний" : "Недоступний",   //якщо евейлбл доступний, інакше недоступний
                            availability_color = color
                        });
                    }
                    reader.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Помилка завантаження списку психологів: " + ex.Message);
            }
        }

        private void ToggleAvailabilityButton_Click(object sender, RoutedEventArgs e)  //2 вкл КНОПОЧКА статусу психол
        {
            Button button = (Button)sender;
            int psychologistId = Convert.ToInt32(button.Tag);    //щоб знати кого конкретно в бд перемикаємо за айді

            try
            {
                using (MySqlConnection conn = DBMySQLUtils.GetDBConnection())
                {
                    conn.Open();
                    string query = "SELECT availability_status FROM psychologists WHERE psychologist_id = @psychologist_id";
                    MySqlCommand checkCmd = new MySqlCommand(query, conn);
                    checkCmd.Parameters.AddWithValue("@psychologist_id", psychologistId);   //обрання психолога
                    string currentStatus = checkCmd.ExecuteScalar().ToString();

                    string newStatus = currentStatus == "available" ? "unavailable" : "available";
                    // Если currentStatus == "available", то newStatus станет "unavailable".
                    // Если currentStatus не "available"(например, "unavailable"), то newStatus станет "available".

                    string updateQuery = "UPDATE psychologists SET availability_status = @new_status WHERE psychologist_id = @psychologist_id";
                    MySqlCommand updateCmd = new MySqlCommand(updateQuery, conn);
                    updateCmd.Parameters.AddWithValue("@new_status", newStatus);           //встановлення нового статуса
                    updateCmd.Parameters.AddWithValue("@psychologist_id", psychologistId);
                    updateCmd.ExecuteNonQuery();

                    LoadPsychologistsList();           // оновлення списку та комбобокусу
                    LoadPsychologists();
                    MessageBox.Show("Статус психолога змінено на " + (newStatus == "available" ? "Доступний" : "Недоступний") + "!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Помилка: " + ex.Message);
            }
        }

        private void PsychologistRightsCheckBox_Checked(object sender, RoutedEventArgs e)  //права психолргів ПЕРЕМИКАЧ
        {
            foreach (var item in PsychologistsListBox.Items)  //для кожного психолога в списку
            {
                var container = PsychologistsListBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem; //контейнер психолога
                if (container != null) //якщо він є то шукаємо кнопочку перемикач статусу
                {
                    var button = FindVisualChild<Button>(container);
                    if (button != null && button.Name == "ToggleAvailabilityButton")
                    {
                        button.IsEnabled = true;    //робимо її клікабельною
                    }
                }
            }
        }
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject //метод шукає нащадка віз. дерева
        {                                                                           //тобто кнопку у середині лістбокс
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) //цикл для усіх лістбоксів(батьків) 
            {
                var child = VisualTreeHelper.GetChild(parent, i);    //дитина під номером і 
                if (child != null && child is T)          //якщо не нул (яку ми шкукаємо) то повертаємл
                {
                    return (T)child;
                }
                else
                {
                    T childOfChild = FindVisualChild<T>(child);                 //якщо ні шукаємо глибше
                    if (childOfChild != null)
                    {
                        return childOfChild;
                    }
                                                /* якщо відразу знаходиш правильну коробку — супер, береш її.
                                                Якщо ні — відкриваєш кожну дитячу коробку і заглядаєш у неї(рекурсія).
                                                Якщо й там нічого — ідеш далі.*/
                }
            }
            return null;
        }

        private void PsychologistComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)  //3 бронь психологи
        {
            UpdateAvailableTimeSlots();
        }

        private void AppointmentDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e) //3 бронь дата
        {
            UpdateAvailableTimeSlots();
        }

        private void UpdateAvailableTimeSlots()  //3 бронь, оновлювач слотів
        {
            TimeSlotComboBox.Items.Clear();
            if (PsychologistComboBox.SelectedItem == null || AppointmentDatePicker.SelectedDate == null)
            {
                return;
            }                       //якщо ніхто не обраний

            DateTime selectedDate = AppointmentDatePicker.SelectedDate.Value;  //обрана дата
            if (selectedDate.DayOfWeek == DayOfWeek.Saturday || selectedDate.DayOfWeek == DayOfWeek.Sunday) //вихідний?
            {
                BookMessage.Text = "Бронювання у вихідні заборонено!";
                TimeSlotComboBox.IsEnabled = false;
                return;
            }

            if (selectedDate < DateTime.Today)
            {
                BookMessage.Text = "Не можна бронювати на минулі дати!";         //не минуле
                TimeSlotComboBox.IsEnabled = false;
                return;
            }

            TimeSlotComboBox.IsEnabled = true;  //вивід активні слоти
            BookMessage.Text = "";

            int psychologistId = ((KeyValuePair<int, string>)PsychologistComboBox.SelectedItem).Key;  //отримали психолога
            List<DateTime> bookedSlots = GetBookedSlots(psychologistId, selectedDate);  //зайняті слоти з бд

            foreach (var slot in WorkingHours)  //перевірка кожного слоту
            {
                DateTime slotStart = selectedDate.Date + slot.Start;      //дата + час
                bool isBooked = false;                                   //зайнятий чи ні
                foreach (DateTime booked in bookedSlots)
                {
                    if (booked.Date == slotStart.Date && booked.TimeOfDay == slot.Start)  //перевірка поточного слоту із зайнятим
                    {
                        isBooked = true;  //якщо збігається брейк
                        break;
                    }
                }
                if (!isBooked)            //якщо слот не зайнятий то додаєсо новий
                {
                    TimeSlotComboBox.Items.Add($"{slot.Start.Hours:00}:{slot.Start.Minutes:00} - {slot.End.Hours:00}:{slot.End.Minutes:00}");
                }
            }

            if (TimeSlotComboBox.Items.Count == 0)       //важкий день психолога
            {
                BookMessage.Text = "Всі слоти зайняті!";
            }
        }

        private List<DateTime> GetBookedSlots(int psychologistId, DateTime date)      //забронювати години
        {
            List<DateTime> bookedSlots = new List<DateTime>();      //список зарезерв слотів
            try
            {
                using (MySqlConnection conn = DBMySQLUtils.GetDBConnection())
                {
                    conn.Open();
                    string query = "SELECT appointment_date FROM appointments WHERE psychologist_id = @psychologist_id AND DATE(appointment_date) = @date AND status = 'pending'";
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@psychologist_id", psychologistId); //задана дата, психолог, пендінг сесія
                    cmd.Parameters.AddWithValue("@date", date.Date);
                    MySqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        DateTime appointmentDate = reader.GetDateTime("appointment_date");  //витягує дату+час
                        bookedSlots.Add(appointmentDate);                    //додаємо до списку
                    }
                    reader.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Помилка при завантаженні слотів: " + ex.Message);
            }
            return bookedSlots;
        }

        private void BookButton_Click(object sender, RoutedEventArgs e)            //3 кнопка бронювання
        {
            if (currentUserId == -1)
            {
                BookMessage.Text = "Спочатку увійдіть у систему!";
                return;
            }
            if (PsychologistComboBox.SelectedItem == null || AppointmentDatePicker.SelectedDate == null || TimeSlotComboBox.SelectedItem == null)
            {
                BookMessage.Text = "Оберіть психолога, дату і час!";
                return;
            }

            DateTime selectedDate = AppointmentDatePicker.SelectedDate.Value;
            if (selectedDate.DayOfWeek == DayOfWeek.Saturday || selectedDate.DayOfWeek == DayOfWeek.Sunday)
            {
                BookMessage.Text = "Бронювання у вихідні неможливе!";
                return;
            }

            if (selectedDate < DateTime.Today)
            {
                BookMessage.Text = "Не можна бронювати на минулі дати!";
                return;
            }

            int psychologistId = ((KeyValuePair<int, string>)PsychologistComboBox.SelectedItem).Key;
            string selectedTimeSlot = TimeSlotComboBox.SelectedItem.ToString();
            string[] timeParts = selectedTimeSlot.Split('-');
            TimeSpan startTime = TimeSpan.Parse(timeParts[0].Trim());
            DateTime appointmentDateTime = selectedDate.Date + startTime;       //створення часу, конверт в 8:00:00 збереження в бд


            List<DateTime> bookedSlots = GetBookedSlots(psychologistId, selectedDate);   //зайняті слоти сптслк
            bool isBooked = false;
            foreach (DateTime booked in bookedSlots)
            {
                if (booked.Date == appointmentDateTime.Date && booked.TimeOfDay == appointmentDateTime.TimeOfDay) //чи зайнятиій
                {
                    isBooked = true;
                    break;
                }
            }
            if (isBooked)  //щоб неможливо було повтрно замовити один і той самий час
            {
                BookMessage.Text = "Слот уже зайнятий!";
                UpdateAvailableTimeSlots();
                return;
            }

            try
            {
                using (MySqlConnection conn = DBMySQLUtils.GetDBConnection())
                {
                    conn.Open();
                    MySqlCommand cmd = new MySqlCommand("BookAppointment", conn);      //ПРОЦЕДУРКА збирає дані)
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@p_user_id", currentUserId);
                    cmd.Parameters.AddWithValue("@p_psychologist_id", psychologistId);
                    cmd.Parameters.AddWithValue("@p_appointment_date", appointmentDateTime);
                    cmd.Parameters.Add("@result_message", MySqlDbType.VarChar, 255).Direction = System.Data.ParameterDirection.Output;

                    cmd.ExecuteNonQuery(); //виконується
                    BookMessage.Text = cmd.Parameters["@result_message"].Value.ToString();
                    Console.WriteLine("Бронь: " + appointmentDateTime + " - " + BookMessage.Text);
                    LoadHistory();
                    LoadPsychologists();
                    LoadPsychologistsList();
                    UpdateAvailableTimeSlots();           
                }
            }
            catch (Exception ex)
            {
                BookMessage.Text = "Помилка: " + ex.Message;
            }
        }

        private void LoadHistory()              //4 історія записів
        {
            if (currentUserId == -1) return;

            HistoryListBox.Items.Clear();
            try
            {
                using (MySqlConnection conn = DBMySQLUtils.GetDBConnection())
                {
                    conn.Open();
                    string query = "SELECT a.appointment_id, a.appointment_date, p.full_name AS psychologist_name, a.status " +
                                  "FROM appointments a JOIN psychologists p ON a.psychologist_id = p.psychologist_id " +
                                  "WHERE a.user_id = @user_id";
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@user_id", currentUserId);
                    MySqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())         //читання кожного рядку
                    {
                        DateTime date = reader.GetDateTime("appointment_date");
                        HistoryListBox.Items.Add(new
                        {
                            appointment_id = reader.GetInt32("appointment_id"),
                            appointment_date = date.ToString("yyyy-MM-dd HH:mm:ss"),
                            psychologist_name = reader.GetString("psychologist_name"),        // виитягує дані
                            status = reader.GetString("status")
                        });
                        Console.WriteLine("Історія: " + date); 
                    }
                    reader.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Помилка історії: " + ex.Message);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)               //4 скачсуванння
        {
            if (currentUserId == -1)
            {
                CancelMessage.Text = "Спочатку увійдіть у систему!";
                return;
            }
            if (HistoryListBox.SelectedItem == null)
            {
                CancelMessage.Text = "Оберіть запис для скасування!";
                return;
            }

            dynamic selected = HistoryListBox.SelectedItem;
            int appointmentId = selected.appointment_id;
            string status = selected.status;

            if (status != "pending")
            {
                CancelMessage.Text = "Цей запис уже завершений або скасований!";
                return;
            }

            try
            {
                using (MySqlConnection conn = DBMySQLUtils.GetDBConnection())
                {
                    conn.Open();
                    MySqlCommand cmd = new MySqlCommand("CancelAppointment", conn);  //процедурка)
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@p_appointment_id", appointmentId);
                    cmd.Parameters.Add("@result_message", MySqlDbType.VarChar, 255).Direction = System.Data.ParameterDirection.Output;
                    cmd.ExecuteNonQuery();
                    CancelMessage.Text = cmd.Parameters["@result_message"].Value.ToString();
                    LoadHistory();            //показує що запис скасовано та оновлює слоти
                    UpdateAvailableTimeSlots();
                }
            }
            catch (Exception ex)
            {
                CancelMessage.Text = "Помилка: " + ex.Message;
            }
        }
        private void CompleteSessionButton_Click(object sender, RoutedEventArgs e) //4 завершення консульт
        {
            if (currentUserId == -1)
            {
                CancelMessage.Text = "Спочатку увійдіть в систему!";
                return;
            }
            if (HistoryListBox.SelectedItem == null)
            {
                CancelMessage.Text = "Оберіть консультацію!";
                return;
            }

            dynamic selected = HistoryListBox.SelectedItem;  //обираємо сесію, її номер її статус
            int appointmentId = selected.appointment_id;
            string status = selected.status;

            if (status != "pending")
            {
                CancelMessage.Text = "Ця консультація вже завершена!"; 
                return;
            }

            try
            {
                using (MySqlConnection conn = DBMySQLUtils.GetDBConnection())
                {
                    conn.Open();
                    string query = "UPDATE appointments SET status = 'completed' WHERE appointment_id = @appointment_id";
                    MySqlCommand cmd = new MySqlCommand(query, conn);               // завершення сесії через апдецйт
                    cmd.Parameters.AddWithValue("@appointment_id", appointmentId);
                    cmd.ExecuteNonQuery();

                    CancelMessage.Text = "Сесія завершена! Залиште, будь ласка, відгук.";
                    LoadHistory();
                    LoadAppointmentsForFeedback();
                    UpdateAvailableTimeSlots();
                }
            }
            catch (Exception ex)
            {
                CancelMessage.Text = "Помилка: " + ex.Message;
            }
        }

        private void LoadAppointmentsForFeedback()                  //5 зустрічі для відгуків
        {
            if (currentUserId == -1) return;

            AppointmentComboBox.Items.Clear();
            try
            {
                using (MySqlConnection conn = DBMySQLUtils.GetDBConnection())
                {
                    conn.Open(); // запит: айді запису + ім'я псих - дата для завершених записів
                    string query = "SELECT a.appointment_id, CONCAT(p.full_name, ' - ', a.appointment_date) AS display " +
                                  "FROM appointments a JOIN psychologists p ON a.psychologist_id = p.psychologist_id " +
                                  "WHERE a.user_id = @user_id AND a.status = 'completed'";
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@user_id", currentUserId);     
                    MySqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        int id = reader.GetInt32("appointment_id"); //айді та дата
                        string display = reader.GetString("display");
                        AppointmentComboBox.Items.Add(new KeyValuePair<int, string>(id, display));
                    }
                    reader.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Помилка завантаження консультацій: " + ex.Message);
            }
        }

        private void SubmitFeedbackButton_Click(object sender, RoutedEventArgs e) //5 підтвердження відгуку
        {
            if (currentUserId == -1)
            {
                FeedbackMessage.Text = "Спочатку увійдіть в систему!";
                return;
            }
            if (AppointmentComboBox.SelectedItem == null || RatingComboBox.SelectedItem == null)
            {
                FeedbackMessage.Text = "Оберіть консультацію і оцінку!";
                return;
            }

            int appointmentId = ((KeyValuePair<int, string>)AppointmentComboBox.SelectedItem).Key; //обираємо зустріч 
            int rating = int.Parse(((ComboBoxItem)RatingComboBox.SelectedItem).Content.ToString());  // і відгук

            try
            {
                using (MySqlConnection conn = DBMySQLUtils.GetDBConnection())
                {
                    conn.Open();
                    string checkQuery = "SELECT COUNT(*) FROM feedback WHERE appointment_id = @appointment_id";
                    MySqlCommand checkCmd = new MySqlCommand(checkQuery, conn);
                    checkCmd.Parameters.AddWithValue("@appointment_id", appointmentId);   //перевірка доданих відгуків за айді
                    long count = (long)checkCmd.ExecuteScalar();
                    if (count > 0)
                    {
                        FeedbackMessage.Text = "Відгук уже є!";
                        return;
                    }

                    string insertQuery = "INSERT INTO feedback (appointment_id, user_id, rating, feedback_date) VALUES (@appointment_id, @user_id, @rating, NOW())";
                    MySqlCommand insertCmd = new MySqlCommand(insertQuery, conn);
                    insertCmd.Parameters.AddWithValue("@appointment_id", appointmentId);
                    insertCmd.Parameters.AddWithValue("@user_id", currentUserId);
                    insertCmd.Parameters.AddWithValue("@rating", rating);
                    insertCmd.ExecuteNonQuery();                            //додавання відгуку

                    FeedbackMessage.Text = "Дякую за відгук!";
                    RatingComboBox.SelectedItem = null;
                    AppointmentComboBox.SelectedItem = null;
                    LoadFeedbackHistory();
                }
            }
            catch (Exception ex)
            {
                FeedbackMessage.Text = "Помилка: " + ex.Message;
            }
        }

        private void LoadFeedbackHistory()           //6 завантажуємо історя відгуки 
        {
            if (currentUserId == -1) return;

            FeedbackHistoryListBox.Items.Clear();  //очищуємо 
            double totalRating = 0;
            int feedbackCount = 0;

            try
            {
                using (MySqlConnection conn = DBMySQLUtils.GetDBConnection())
                {
                    conn.Open();
                    string query = "SELECT f.feedback_id, f.appointment_id, f.rating, f.feedback_date, CONCAT(p.full_name, ' - ', a.appointment_date) AS display_text " +
                                  "FROM feedback f JOIN appointments a ON f.appointment_id = a.appointment_id " +
                                  "JOIN psychologists p ON a.psychologist_id = p.psychologist_id " +
                                  "WHERE f.user_id = @user_id";
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@user_id", currentUserId);
                    MySqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())                                //перебираємо кожен рядок 
                    {
                        FeedbackHistoryListBox.Items.Add(new
                        {
                            display_text = reader.GetString("display_text"),  //ім'я - дата, рейтинг
                            rating = reader.GetInt32("rating"),
                            feedback_date = reader.GetDateTime("feedback_date").ToString("yyyy-MM-dd HH:mm:ss")
                        });
                        totalRating += reader.GetInt32("rating");  //загал рейтинш консульт користувача, збільш лічильник
                        feedbackCount++;
                    }
                    reader.Close();

                    if (feedbackCount > 0)        //рахуємо сер арифм або відгуків немає
                    {
                        double avg = totalRating / feedbackCount;
                        SatisfactionLevelTextBlock.Text = "Середній бал: " + avg.ToString("0.0") + " (" + feedbackCount + " відгуків)";
                    }
                    else
                    {
                        SatisfactionLevelTextBlock.Text = "Відгуків немає.";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Помилка історії відгуків: " + ex.Message);
            }
        }

    }
}