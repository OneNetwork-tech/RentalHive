using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RentalHive.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    personal_number_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    personal_number_cipher = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: false),
                    address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    security_stamp = table.Column<Guid>(type: "uuid", nullable: false),
                    member_since = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    failed_login_count = table.Column<int>(type: "integer", nullable: false),
                    lockout_end_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rental_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    owner_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    daily_rate = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    item_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    boat_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    length_in_feet = table.Column<int>(type: "integer", nullable: true),
                    capacity = table.Column<int>(type: "integer", nullable: true),
                    requires_license = table.Column<bool>(type: "boolean", nullable: true),
                    make = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    model = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    year = table.Column<int>(type: "integer", nullable: true),
                    number_of_seats = table.Column<int>(type: "integer", nullable: true),
                    transmission = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    fuel_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    payload_capacity_in_kg = table.Column<double>(type: "double precision", nullable: true),
                    has_tail_lift = table.Column<bool>(type: "boolean", nullable: true),
                    is_wheelchair_accessible = table.Column<bool>(type: "boolean", nullable: true),
                    load_capacity_in_cubic_meters = table.Column<double>(type: "double precision", nullable: true),
                    has_sliding_door = table.Column<bool>(type: "boolean", nullable: true),
                    machine_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    horsepower = table.Column<int>(type: "integer", nullable: true),
                    weight_in_kg = table.Column<double>(type: "double precision", nullable: true),
                    load_capacity_in_kg = table.Column<double>(type: "double precision", nullable: true),
                    deck_width_in_meters = table.Column<double>(type: "double precision", nullable: true),
                    deck_length_in_meters = table.Column<double>(type: "double precision", nullable: true),
                    has_brakes = table.Column<bool>(type: "boolean", nullable: true),
                    max_boat_length_in_feet = table.Column<int>(type: "integer", nullable: true),
                    is_submersible = table.Column<bool>(type: "boolean", nullable: true),
                    number_of_axles = table.Column<int>(type: "integer", nullable: true),
                    has_winch = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rental_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_rental_items_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    rental_item_id = table.Column<int>(type: "integer", nullable: false),
                    renter_id = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    days = table.Column<int>(type: "integer", nullable: false),
                    daily_rate = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    total_cost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bookings", x => x.id);
                    table.CheckConstraint("ck_bookings_days", "days = end_date - start_date");
                    table.CheckConstraint("ck_bookings_period", "end_date > start_date");
                    table.ForeignKey(
                        name: "fk_bookings_rental_items_rental_item_id",
                        column: x => x.rental_item_id,
                        principalTable: "rental_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bookings_users_renter_id",
                        column: x => x.renter_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "item_images",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    rental_item_id = table.Column<int>(type: "integer", nullable: false),
                    file_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    content_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item_images", x => x.id);
                    table.ForeignKey(
                        name: "fk_item_images_rental_items_rental_item_id",
                        column: x => x.rental_item_id,
                        principalTable: "rental_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reviews",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    booking_id = table.Column<int>(type: "integer", nullable: false),
                    rental_item_id = table.Column<int>(type: "integer", nullable: false),
                    author_id = table.Column<int>(type: "integer", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reviews", x => x.id);
                    table.CheckConstraint("ck_reviews_rating", "rating BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_reviews_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reviews_rental_items_rental_item_id",
                        column: x => x.rental_item_id,
                        principalTable: "rental_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reviews_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_rental_item_id_status",
                table: "bookings",
                columns: new[] { "rental_item_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_renter_id",
                table: "bookings",
                column: "renter_id");

            migrationBuilder.CreateIndex(
                name: "ix_item_images_file_name",
                table: "item_images",
                column: "file_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_item_images_rental_item_id",
                table: "item_images",
                column: "rental_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_rental_items_city",
                table: "rental_items",
                column: "city");

            migrationBuilder.CreateIndex(
                name: "ix_rental_items_item_type_is_active",
                table: "rental_items",
                columns: new[] { "item_type", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_rental_items_owner_id",
                table: "rental_items",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_author_id",
                table: "reviews",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_booking_id",
                table: "reviews",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reviews_rental_item_id",
                table: "reviews",
                column: "rental_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_personal_number_hash",
                table: "users",
                column: "personal_number_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_phone_number",
                table: "users",
                column: "phone_number",
                unique: true);

            // No two confirmed bookings of the same item may overlap. The period is
            // half-open [start_date, end_date): the return day can be the next pick-up day.
            migrationBuilder.Sql(
                $"ALTER TABLE bookings ADD CONSTRAINT {RentalHive.Infrastructure.Persistence.RentalHiveDbContext.NoOverlapConstraint} " +
                "EXCLUDE USING gist (rental_item_id WITH =, daterange(start_date, end_date, '[)') WITH &&) WHERE (status = 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "item_images");

            migrationBuilder.DropTable(
                name: "reviews");

            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropTable(
                name: "rental_items");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
