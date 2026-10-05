import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { Register } from './register';

describe('Register', () => {
  let fixture: ComponentFixture<Register>;
  let http: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Register],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(Register);
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  // Types into the inputs and submits the form the way a user would.
  function submit(email: string, password: string): void {
    const element = fixture.nativeElement as HTMLElement;
    const [emailInput, passwordInput] = Array.from(element.querySelectorAll('input'));
    emailInput.value = email;
    emailInput.dispatchEvent(new Event('input'));
    passwordInput.value = password;
    passwordInput.dispatchEvent(new Event('input'));
    element.querySelector('form')?.dispatchEvent(new Event('submit'));
  }

  it('signs the new user in and goes to the workspace', () => {
    submit('ana@test.local', 'Passw0rd!');

    // Identity's /register doesn't sign in, so Auth logs in right after.
    http.expectOne((req) => req.url.endsWith('/auth/register')).flush(null);
    http.expectOne((req) => req.url.endsWith('/auth/login')).flush(null);
    http
      .expectOne((req) => req.url.endsWith('/auth/manage/info'))
      .flush({ email: 'ana@test.local', isEmailConfirmed: false });

    expect(router.navigate).toHaveBeenCalledWith(['/']);
  });

  it('lists every rule the password breaks', async () => {
    submit('ana@test.local', 'weak');

    http
      .expectOne((req) => req.url.endsWith('/auth/register'))
      .flush(
        {
          title: 'One or more validation errors occurred.',
          errors: {
            PasswordTooShort: ['Passwords must be at least 6 characters.'],
            PasswordRequiresUpper: ["Passwords must have at least one uppercase ('A'-'Z')."],
          },
        },
        { status: 400, statusText: 'Bad Request' },
      );
    await fixture.whenStable();

    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('at least 6 characters');
    expect(text).toContain('at least one uppercase');
  });
});
