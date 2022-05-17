using QLab.Robot.Bankrupt.Efrsb.MessageService.Dto;
using System.Xml;

namespace QLab.Robot.Bankrupt.Efrsb.MessageService.Services
{
    internal static class MessageParser
    {
        internal static void Parse(string messageContent, out Message? message)
        {
            XmlDocument xDoc = new();
            xDoc.LoadXml(messageContent);

            XmlElement? xRoot = xDoc.DocumentElement;

            if (xRoot != null)
            {
                message = new();

                message.EfrsbId = xRoot.SelectSingleNode("Id")!.InnerText;
                message.PublishDate = DateTime.Parse(xRoot.SelectSingleNode("PublishDate")!.InnerText);

                var bankruptNode = xRoot.SelectSingleNode("Bankrupt");
                if (bankruptNode != null)
                {
                    ParseBankrupt(bankruptNode, out var bankruptInfo);
                    message.BankruptInfo = bankruptInfo;

                    //
                    var messageNode = xRoot.SelectSingleNode("MessageInfo");
                    if (messageNode != null)
                    {
                        ParseMessageInfo(messageNode, out var messageInfo);
                        message.MessageType = messageInfo?.MessageType;
                        message.Text = messageInfo?.Text;
                    }
                }

                return;
            }

            message = null;
        }

        private static void ParseMessageInfo(XmlNode xmlNode, out MessageInfo? messageInfo)
        {
            if (xmlNode != null)
            {
                var messageType = xmlNode.Attributes?.GetNamedItem("MessageType")?.Value;

                messageInfo = new MessageInfo
                {
                    MessageType = messageType,
                    Text = xmlNode.InnerText,
                };

                return;
            }

            messageInfo = null;
        }

        private static void ParseBankrupt(XmlNode xmlNode, out BankruptInfo? bankruptInfo)
        {
            if (xmlNode != null)
            {
                var bankruptCode = xmlNode.Attributes?.GetNamedItem("xsi:type")?.Value;
                if (!string.IsNullOrEmpty(bankruptCode))
                {
                    if (bankruptCode.StartsWith("Bankrupt.Company"))
                    {
                        bankruptInfo = new BankruptCompany
                        {
                            Name = xmlNode.SelectSingleNode("Name")?.InnerText,
                            Address = xmlNode.SelectSingleNode("Address")?.InnerText,
                            Inn = xmlNode.SelectSingleNode("Inn")?.InnerText,
                            Ogrn = xmlNode.SelectSingleNode("Ogrn")?.InnerText,
                        };
                        return;
                    }
                    else if (bankruptCode.StartsWith("Bankrupt.Person"))
                    {
                        _ = DateTime.TryParse(xmlNode.SelectSingleNode("Birthdate")?.InnerText, out var birthdate);
                        var fio = xmlNode.SelectSingleNode("Fio");

                        bankruptInfo = new BankruptPerson()
                        {

                            Fio = GetFio(fio),
                            FioHistory = GetFioHistory(xmlNode.SelectSingleNode("FioHistory")),

                            Address = xmlNode.SelectSingleNode("Address")?.InnerText,
                            Birthdate = birthdate,
                            Birthplace = xmlNode.SelectSingleNode("Birthplace")?.InnerText,
                            Inn = xmlNode.SelectSingleNode("Inn")?.InnerText,
                            Ogrnip = xmlNode.SelectSingleNode("Ogrnip")?.InnerText,
                            Snils = xmlNode.SelectSingleNode("Snils")?.InnerText,
                        };
                        return;
                    }
                }
            }

            bankruptInfo = null;
        }

        private static Fio? GetFio(XmlNode? xmlNode)
        {
            if (xmlNode == null)
                return null;

            var fio = new Fio
            {
                FirstName = xmlNode?.SelectSingleNode("FirstName")?.InnerText,
                LastName = xmlNode?.SelectSingleNode("LastName")?.InnerText,
                MiddleName = xmlNode?.SelectSingleNode("MiddleName")?.InnerText,
            };

            return fio;
        }

        private static List<Fio>? GetFioHistory(XmlNode? xmlNode)
        {
            if (xmlNode == null)
                return null;

            var list = new List<Fio>();
            foreach (var node in xmlNode.ChildNodes)
            {
                Fio? fio = GetFio((XmlNode)node);
                if (fio != null)
                    list.Add(fio);
            }
            return list;
        }
    }
}
