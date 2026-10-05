import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { Login } from './login';

describe('Login', () => {
  let fixture: ComponentFixture<Login>;
  let http: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(Login);
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

  it('goes to the workspace after a successful login', () => {
    submit('ana@test.local', 'Passw0rd!');

    http.expectOne((req) => req.url.endsWith('/auth/login')).flush(null);
    http
      .expectOne((req) => req.url.endsWith('/auth/manage/info'))
      .flush({ email: 'ana@test.local', isEmailConfirmed: false });

    expect(router.navigate).toHaveBeenCalledWith(['/']);
  });

  it('shows an error when the credentials are wrong', async () => {
    submit('ana@test.local', 'wrong');

    http
      .expectOne((req) => req.url.endsWith('/auth/login'))
      .flush({ detail: 'Failed' }, { status: 401, statusText: 'Unauthorized' });
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'Incorrect email or password.',
    );
  });
});
