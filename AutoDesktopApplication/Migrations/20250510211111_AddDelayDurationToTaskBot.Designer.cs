﻿// <auto-generated />
using System;
using AutoDesktopApplication.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace AutoDesktopApplication.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20250510211111_AddDelayDurationToTaskBot")]
    partial class AddDelayDurationToTaskBot
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "8.0.4");

            modelBuilder.Entity("AutoDesktopApplication.Models.Project", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("CreatedDate")
                        .HasColumnType("TEXT");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("ModifiedDate")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Projects");
                });

            modelBuilder.Entity("AutoDesktopApplication.Models.TaskBot", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("AiDecisionCriteria")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("CreatedDate")
                        .HasColumnType("TEXT");

                    b.Property<long>("DelayBefore")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<long>("EstimatedDuration")
                        .HasColumnType("INTEGER");

                    b.Property<string>("InputData")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("ModifiedDate")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<byte[]>("ScreenshotData")
                        .HasColumnType("BLOB");

                    b.Property<int>("SequenceOrder")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Type")
                        .HasColumnType("INTEGER");

                    b.Property<int>("WorkflowId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("WorkflowId");

                    b.ToTable("TaskBots");
                });

            modelBuilder.Entity("AutoDesktopApplication.Models.Workflow", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("CreatedDate")
                        .HasColumnType("TEXT");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("LastRunDate")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("ModifiedDate")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("ProjectId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("ProjectId");

                    b.ToTable("Workflows");
                });

            modelBuilder.Entity("AutoDesktopApplication.Models.WorkflowRun", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("EndTime")
                        .HasColumnType("TEXT");

                    b.Property<string>("Notes")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("StartTime")
                        .HasColumnType("TEXT");

                    b.Property<bool>("Successful")
                        .HasColumnType("INTEGER");

                    b.Property<int>("WorkflowId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("WorkflowId");

                    b.ToTable("WorkflowRuns");
                });

            modelBuilder.Entity("AutoDesktopApplication.Models.TaskBot", b =>
                {
                    b.HasOne("AutoDesktopApplication.Models.Workflow", "Workflow")
                        .WithMany("TaskBots")
                        .HasForeignKey("WorkflowId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Workflow");
                });

            modelBuilder.Entity("AutoDesktopApplication.Models.Workflow", b =>
                {
                    b.HasOne("AutoDesktopApplication.Models.Project", "Project")
                        .WithMany("Workflows")
                        .HasForeignKey("ProjectId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Project");
                });

            modelBuilder.Entity("AutoDesktopApplication.Models.WorkflowRun", b =>
                {
                    b.HasOne("AutoDesktopApplication.Models.Workflow", "Workflow")
                        .WithMany("WorkflowRuns")
                        .HasForeignKey("WorkflowId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Workflow");
                });

            modelBuilder.Entity("AutoDesktopApplication.Models.Project", b =>
                {
                    b.Navigation("Workflows");
                });

            modelBuilder.Entity("AutoDesktopApplication.Models.Workflow", b =>
                {
                    b.Navigation("TaskBots");

                    b.Navigation("WorkflowRuns");
                });
#pragma warning restore 612, 618
        }
    }
}
