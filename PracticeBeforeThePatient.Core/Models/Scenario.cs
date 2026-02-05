using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PracticeBeforeThePatient.Core.Models;

public class Scenario
{
    public string Title { get; set; } = "";
    public Node Root { get; set; } = new();
}
