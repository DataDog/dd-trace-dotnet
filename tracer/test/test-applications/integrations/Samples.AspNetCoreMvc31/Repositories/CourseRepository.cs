using NHibernate;
using NHibernate.Mapping.ByCode;
using NHibernate.Mapping.ByCode.Conformist;
using WebService.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Cfg;

namespace WebService.Repositories
{
    public class CourseRepository
    {
        private static ISessionFactory _sessionFactory;

        static CourseRepository()
        {
            try
            {
                _sessionFactory = ConfigureNHibernate();
                SeedData();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private static ISessionFactory ConfigureNHibernate()
        {
            var cfg = new Configuration();

            cfg.DataBaseIntegration(db =>
            {
                db.ConnectionString = "Data Source=app.db";
                db.Driver<NHibernate.Driver.SQLite20Driver>();
                db.Dialect<NHibernate.Dialect.SQLiteDialect>();
                db.ConnectionProvider<NHibernate.Connection.DriverConnectionProvider>();
                db.SchemaAction = SchemaAutoAction.Create;
            });

            var mapper = new ModelMapper();
            mapper.AddMapping<CourseMap>();

            cfg.AddMapping(mapper.CompileMappingForAllExplicitlyAddedEntities());

            return cfg.BuildSessionFactory();
        }

        private static void SeedData()
        {
            using (var session = _sessionFactory.OpenSession())
            using (var transaction = session.BeginTransaction())
            {
                var courses = new List<Course>
                {
                    new Course
                    {
                        Name = "Intoduction to superheroes",
                        Code = "INTRO-001",
                        StudentCount = 300,
                        LectureRequiredFeatures =
                            new HashSet<string> { LectureFeatures.Projector, LectureFeatures.Wifi },
                        PracticeRequiredFeatures =
                            new HashSet<string> { LectureFeatures.Projector, LectureFeatures.Wifi },
                        TeacherCount = 2,
                        TeacherAssistantCount = 10
                    },
                    new Course
                    {
                        Name = "Legalities for vigilantes",
                        Code = "LEGAL-001",
                        StudentCount = 100,
                        LectureRequiredFeatures =
                            new HashSet<string> { LectureFeatures.Projector, LectureFeatures.Wifi },
                        PracticeRequiredFeatures =
                            new HashSet<string> { LectureFeatures.Projector, LectureFeatures.Wifi },
                        TeacherCount = 2,
                        TeacherAssistantCount = 4
                    },
                    new Course
                    {
                        Name = "Hand combat for beginners",
                        Code = "HAND-001",
                        StudentCount = 100,
                        LectureRequiredFeatures = new HashSet<string>(),
                        PracticeRequiredFeatures = new HashSet<string> { LectureFeatures.SoundProof },
                        TeacherCount = 2,
                        TeacherAssistantCount = 4
                    },
                    new Course
                    {
                        Name = "Hand combat for level 2",
                        Code = "HAND-002",
                        StudentCount = 50,
                        LectureRequiredFeatures = new HashSet<string>(),
                        PracticeRequiredFeatures = new HashSet<string> { LectureFeatures.SoundProof },
                        TeacherCount = 1,
                        TeacherAssistantCount = 3
                    },
                    new Course
                    {
                        Name = "Powered Flight",
                        Code = "FLIGHT-001",
                        StudentCount = 20,
                        LectureRequiredFeatures =
                            new HashSet<string> { LectureFeatures.VR, LectureFeatures.StaticShield },
                        PracticeRequiredFeatures =
                            new HashSet<string> { LectureFeatures.SoundProof, LectureFeatures.StaticShield },
                        TeacherCount = 1,
                        TeacherAssistantCount = 2
                    },
                    new Course
                    {
                        Name = "Advance electrical attacks",
                        Code = "AEA-400",
                        StudentCount = 20,
                        LectureRequiredFeatures =
                            new HashSet<string> { LectureFeatures.HighPower, LectureFeatures.Projector },
                        PracticeRequiredFeatures = new HashSet<string>
                        {
                            LectureFeatures.HighPower, LectureFeatures.Wifi
                        },
                        TeacherCount = 1,
                        TeacherAssistantCount = 3
                    }
                };

                foreach (var course in courses)
                {
                    session.Save(course);
                }

                transaction.Commit();
            }
        }

        public static IEnumerable<Course> GetCourses()
        {
            using (var session = _sessionFactory.OpenSession())
            {
                return session.Query<Course>().ToList();
            }
        }

        public static Course GetCourseById(string id)
        {
            using (var session = _sessionFactory.OpenSession())
            {
                return session.Query<Course>().FirstOrDefault(course => course.Code == id);
            }
        }
    }

    public class CourseMap : ClassMapping<Course>
    {
        public CourseMap()
        {
            Table("Courses");
            Id(x => x.Id, m => m.Generator(Generators.Identity));
            Property(x => x.Name);
            Property(x => x.Code);
            Property(x => x.StudentCount);
            Property(x => x.TeacherCount);
            Property(x => x.TeacherAssistantCount);
            Set(x => x.LectureRequiredFeatures, c =>
            {
                c.Table("LectureRequiredFeatures");
                c.Cascade(Cascade.All);
                c.Key(k => k.Column("CourseId"));
            }, r => r.Element(e => e.Column("Feature")));
            Set(x => x.PracticeRequiredFeatures, c =>
            {
                c.Table("PracticeRequiredFeatures");
                c.Cascade(Cascade.All);
                c.Key(k => k.Column("CourseId"));
            }, r => r.Element(e => e.Column("Feature")));
        }
    }
}
