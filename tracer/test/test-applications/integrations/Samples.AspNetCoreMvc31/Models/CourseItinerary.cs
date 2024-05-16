using System;
using System.Collections.Generic;

namespace WebService.Models
{
    public class CourseItinerary
    {
        public List<SessionDescription> Lectures { get; set; }

        public List<SessionDescription> Practices { get; set; }

        public List<AssignmentDescription> Assignments { get; set; }
    }

    public class SessionDescription
    {
        public int SessionNumber { get; set; }
        public DateTime Date { get; set; }

        public string Title { get; set; }
        public string Description { get; set; }
    }

    public class AssignmentDescription
    {
        public int AssignmnetNumber { get; set; }
        public DateTime PublishDate { get; set; }
        public DateTime DueDate { get; set; }

        public string Title { get; set; }
        public string Description { get; set; }
    }
}
